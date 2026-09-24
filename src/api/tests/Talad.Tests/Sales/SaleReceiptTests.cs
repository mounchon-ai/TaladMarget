using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Catalog;
using Talad.Api.Sales;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Sales;

/// <summary>
/// UC-talad-005 · API-011 — the receipt of a bill the caller sold, read back from what the bill kept. The full-screen
/// receipt with [พิมพ์] [ปิด] is FE-talad-036's; the history, every bill for the owner (ACL-013) and the reprint are
/// FE-talad-037 · 039's. Voiding is UC-talad-025's, so AC-talad-032's voided bill cannot be made here — only that a new
/// cart's bill has its own number and names no other bill.
/// </summary>
[Trait("feature", "FE-talad-035")]
public sealed class SaleReceiptTests : IDisposable
{
    private readonly TaladApiFactory _app = new();

    public void Dispose() => _app.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private T Db<T>(Func<TaladDbContext, T> work)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var result = work(db);
        db.SaveChanges();
        return result;
    }

    private async Task<HttpClient> SignedInAs(string username, string displayName, UserRole role)
    {
        Db(db =>
        {
            using var scope = _app.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IdentityPasswordHasher>();
            var account = new UserAccount(username, displayName, role, DateTimeOffset.UnixEpoch);
            account.SetPasswordHash(hasher.Hash(account, "Pass#2569"));
            return db.UserAccounts.Add(account);
        });
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> Owner() => SignedInAs("owner", "เจ้าของร้าน", UserRole.Owner);
    private Task<HttpClient> Somchai() => SignedInAs("somchai", "สมชาย", UserRole.Cashier);
    private Task<HttpClient> Manee() => SignedInAs("manee", "มานี", UserRole.Cashier);

    private int Product(string name, decimal price, int stock)
    {
        var product = Db(db =>
        {
            var p = new Product(name, null, stock, 0, DateTimeOffset.UnixEpoch);
            db.Products.Add(p);
            return p;
        });
        Db(db =>
        {
            var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
            var version = new ProductPriceVersion(product.Id, price, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
            db.ProductPriceVersions.Add(version);
            db.SaveChanges();
            db.Products.Single(p => p.Id == product.Id).PointAtPrice(version);
            return version;
        });
        return product.Id;
    }

    private static async Task<int> Promotion(HttpClient owner, object body)
    {
        var response = await owner.PostAsJsonAsync("/api/promotions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Talad.Application.Promotions.PromotionView>())!.Id;
    }

    private static async Task<int> Member(HttpClient cashier, string name, string phone)
    {
        var response = await cashier.PostAsJsonAsync("/api/members", new { name, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Talad.Application.Members.MemberView>())!.Id;
    }

    private static async Task MemberRate(HttpClient owner, int rate) =>
        Assert.True((await owner.PostAsJsonAsync("/api/settings/member-discount", new { ratePercent = rate })).IsSuccessStatusCode);

    private static async Task<int> Put(HttpClient cashier, int product, int qty)
    {
        Assert.Equal(HttpStatusCode.OK, (await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(product))).StatusCode);
        var cart = qty > 1
            ? await cashier.PatchAsJsonAsync($"/api/cart/lines/{product}", new SetQtyRequest(qty))
            : await cashier.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, cart.StatusCode);
        return (await cart.Content.ReadFromJsonAsync<CartView>())!.Id;
    }

    private static async Task<SaleView> Pay(HttpClient cashier, int cart)
    {
        var response = await cashier.PostAsJsonAsync("/api/cart/checkout", new CheckoutRequest(cart, []));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleView>())!;
    }

    private static async Task<SaleView> Receipt(HttpClient caller, int sale)
    {
        var response = await caller.GetAsync($"/api/sales/{sale}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleView>())!;
    }

    /// <summary>SaleView holds a list, so record equality would compare references — compare what the wire carries.</summary>
    private static string Wire(SaleView view) => JsonSerializer.Serialize(view);

    // ── AC-talad-004 · 029 · 030 — the receipt of the bill just paid ──────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-004"), Trait("ac", "AC-talad-029"), Trait("ac", "AC-talad-030")]
    public async Task Somchai_opens_the_receipt_of_the_90_baht_bill_he_just_paid()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var paid = await Pay(somchai, await Put(somchai, orange, 2));

        var receipt = await Receipt(somchai, paid.Id);

        Assert.Equal((paid.ReceiptNo, "สมชาย", 90m, 90m, "PAID"), (receipt.ReceiptNo, receipt.SellerName, receipt.Subtotal, receipt.NetTotal, receipt.Status));
        var line = Assert.Single(receipt.Lines);
        Assert.Equal((1, "ส้มสายน้ำผึ้ง", 2, 45m, 90m), (line.LineNo, line.Name, line.Qty, line.UnitPrice, line.LineNet));
        Assert.Null(receipt.Member);
        Assert.Equal((0, 0m, 0m, 0m), (receipt.MemberRate, receipt.MemberDiscount, receipt.BillDiscount, receipt.PromoDiscountTotal));
    }

    [Fact, Trait("ac", "AC-talad-004")]
    public async Task The_receipt_is_the_same_bill_checkout_answered_down_to_every_field()
    {
        // a member at 5% · a line promotion · a free gift · a whole-bill promotion — everything a receipt can carry
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var mangosteen = Product("มังคุด แพ็ก", 120m, 5);
        var milk = Product("นมจืด", 30m, 10);
        var somchai = await Somchai();
        await MemberRate(owner, 5);
        var member = await Member(somchai, "สมหญิง ใจดี", "0812345678");
        await Promotion(owner, new { name = "ซื้อ ส้ม 2 แถม มังคุด 1", type = "BUY_X_GET_Y", productA = orange, qtyA = 2, freeProduct = mangosteen, freeQty = 1, startDate = "2026-09-01" });
        await Promotion(owner, new { name = "นมจืด ลด 10%", type = "ITEM_PERCENT", productA = milk, ratePercent = 10, startDate = "2026-09-01" });
        await Promotion(owner, new { name = "ลดทั้งบิล 5%", type = "BILL_PERCENT", ratePercent = 5, minSubtotal = 0, startDate = "2026-09-01" });
        await Put(somchai, orange, 2);
        await Put(somchai, mangosteen, 1);
        var cart = await Put(somchai, milk, 3);
        Assert.Equal(HttpStatusCode.OK, (await somchai.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(member))).StatusCode);

        var paid = await Pay(somchai, cart);
        var receipt = await Receipt(somchai, paid.Id);

        Assert.NotNull(paid.BillPromotion);
        Assert.Contains(paid.Lines, l => l.FreeQty == 1);
        Assert.Contains(paid.Lines, l => l.Promotion?.Name == "นมจืด ลด 10%");
        Assert.Equal(5, paid.MemberRate);
        Assert.Equal(Wire(paid), Wire(receipt));
    }

    // ── BR-talad-035@v1 · 036@v1 · 040@v2 — what the bill kept, not what is true now ──────────────────────

    [Fact, Trait("ac", "AC-talad-004")]
    public async Task Changes_after_the_sale_do_not_reach_its_receipt()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        await MemberRate(owner, 5);
        var member = await Member(somchai, "สมหญิง ใจดี", "0812345678");
        // an earlier promotion revised once, so the orange's promotion id and the id of the version the bill keeps differ
        var milk = Product("นมจืด", 30m, 10);
        var earlier = await Promotion(owner, new { name = "นมจืด ลด 5%", type = "ITEM_PERCENT", productA = milk, ratePercent = 5, startDate = "2026-09-01" });
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/promotions/{earlier}/versions",
            new { name = "นมจืด ลด 6%", type = "ITEM_PERCENT", productA = milk, ratePercent = 6, startDate = "2026-09-01" })).StatusCode);
        var promotion = await Promotion(owner, new { name = "ส้ม ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" });
        var cart = await Put(somchai, orange, 2);
        Assert.Equal(HttpStatusCode.OK, (await somchai.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(member))).StatusCode);
        var paid = await Pay(somchai, cart);

        // the owner renames and then stops the promotion, reprices the orange, changes the member rate, hides the member
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/promotions/{promotion}/versions",
            new { name = "ส้ม ลด 20%", type = "ITEM_PERCENT", productA = orange, ratePercent = 20, startDate = "2026-09-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/promotions/{promotion}/discontinue", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/products/{orange}/prices", new RepriceRequest(50m, "STOCK_SCREEN"))).StatusCode);
        await MemberRate(owner, 10);
        Assert.True((await owner.PostAsync($"/api/members/{member}/hide", null)).IsSuccessStatusCode);

        var receipt = await Receipt(somchai, paid.Id);

        var line = Assert.Single(receipt.Lines);
        Assert.Equal((45m, "ส้ม ลด 10%", promotion, 9m), (line.UnitPrice, line.Promotion!.Name, line.Promotion.Id, line.PromoDiscount));
        Assert.Equal(("สมหญิง ใจดี", 5), (receipt.Member!.Name, receipt.MemberRate));
        Assert.Equal(paid.NetTotal, receipt.NetTotal);
    }

    [Fact, Trait("ac", "AC-talad-004")]
    public async Task A_discontinued_product_still_names_itself_on_the_receipt()
    {
        var owner = await Owner();
        var bread = Product("ขนมปังโฮลวีท", 35m, 10);
        var somchai = await Somchai();
        var paid = await Pay(somchai, await Put(somchai, bread, 1));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/products/{bread}/discontinue", null)).StatusCode);

        Assert.Equal("ขนมปังโฮลวีท", Assert.Single((await Receipt(somchai, paid.Id)).Lines).Name);
    }

    // ── AC-talad-032 · BR-talad-026@v1 — a new bill, a new number ────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-032")]
    public async Task The_next_bill_has_its_own_number_and_names_no_other_bill()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var first = await Pay(owner, await Put(owner, orange, 2));
        var second = await Pay(owner, await Put(owner, orange, 1));

        var receipt = await Receipt(owner, second.Id);

        Assert.NotEqual(first.ReceiptNo, receipt.ReceiptNo);
        Assert.Equal(45m, receipt.NetTotal);
        Assert.DoesNotContain(first.ReceiptNo, Wire(receipt));
    }

    // ── ACL-005 · BR-talad-020@v1 · NFR-talad-005 — whose receipt ────────────────────────────────────────

    [Fact]
    public async Task Another_sellers_bill_and_a_bill_that_does_not_exist_are_not_found()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var manee = await Manee();
        var paid = await Pay(somchai, await Put(somchai, orange, 1));

        var theirs = await manee.GetAsync($"/api/sales/{paid.Id}");
        var none = await somchai.GetAsync($"/api/sales/{paid.Id + 100}");

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (theirs.StatusCode, none.StatusCode));
        Assert.Equal("SALE_NOT_FOUND", (await theirs.Content.ReadFromJsonAsync<CartError>())!.Code);
        Assert.DoesNotContain(paid.ReceiptNo, await theirs.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Without_a_token_the_receipt_is_401()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var paid = await Pay(somchai, await Put(somchai, orange, 1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync($"/api/sales/{paid.Id}")).StatusCode);
    }

    [Fact, Trait("ac", "AC-talad-004")]
    public async Task Checkout_answers_where_the_receipt_is()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 1);

        var response = await somchai.PostAsJsonAsync("/api/cart/checkout", new CheckoutRequest(cart, []));
        var location = response.Headers.Location!;

        Assert.Equal(HttpStatusCode.OK, (await somchai.GetAsync(location)).StatusCode);
    }
}
