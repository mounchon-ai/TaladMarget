using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Sales;
using Talad.Application;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Sales;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Sales;

/// <summary>
/// UC-talad-003 · API-010 — the caller pays their own cart: one bill that keeps every version it was priced with, the
/// stock it takes (free pieces too), the member's accumulated amount and the cart PAID, in one save. Priced by the same
/// reading API-009 shows. The receipt screen is UC-talad-005's (FE-talad-035 · 036); the sales history that "opens this
/// bill" in AC-talad-037..039 is FE-talad-037's — here the bill is read from the database it was written to. The race
/// between two sales, or a sale and an adjustment, on PostgreSQL is proven on the real database; InMemory proves the
/// concurrency check is wired.
/// </summary>
[Trait("feature", "FE-talad-033")]
public sealed class CheckoutTests : IDisposable
{
    /// <summary>Starts at the real now — tokens are checked against the real clock — and is moved after sign-in.</summary>
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }

    private static DateTimeOffset Thai(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));

    private readonly TaladApiFactory _base = new();
    private readonly Clock _clock = new();
    private readonly WebApplicationFactory<Program> _app;

    public CheckoutTests() =>
        _app = _base.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(_clock)));

    public void Dispose()
    {
        _app.Dispose();
        _base.Dispose();
    }

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

    private int Member(string name, string phone, decimal accumulated = 0m)
    {
        var id = Db(db =>
        {
            var m = Talad.Domain.Members.Member.Register(name, phone, 1, DateTimeOffset.UnixEpoch);
            db.Members.Add(m);
            return m;
        }).Id;
        if (accumulated > 0)
            Db(db =>
            {
                db.Members.Single(m => m.Id == id).Accumulate(accumulated); // what earlier bills added
                return 0;
            });
        return id;
    }

    private static async Task<int> Promotion(HttpClient owner, object body)
    {
        var response = await owner.PostAsJsonAsync("/api/promotions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Talad.Application.Promotions.PromotionView>())!.Id;
    }

    private static object Percent(string name, int a, int rate, string start = "2026-09-01", string? end = null) =>
        new { name, type = "ITEM_PERCENT", productA = a, ratePercent = rate, startDate = start, endDate = end };

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

    private static async Task Bind(HttpClient cashier, int member) =>
        Assert.Equal(HttpStatusCode.OK, (await cashier.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(member))).StatusCode);

    private static Task<HttpResponseMessage> Pay(HttpClient cashier, int cart, params int[] choice) =>
        cashier.PostAsJsonAsync("/api/cart/checkout", new CheckoutRequest(cart, choice));

    private static async Task<SaleView> Paid(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleView>())!;
    }

    private static async Task<CheckoutError> Refused(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.Conflict)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutError>())!;
    }

    private List<Sale> Sales() => Db(db => db.Sales.AsNoTracking().Include(s => s.Lines).OrderBy(s => s.Id).ToList());
    private int Stock(int product) => Db(db => db.Products.AsNoTracking().Single(p => p.Id == product).StockQty);
    private decimal Accumulated(int member) => Db(db => db.Members.AsNoTracking().Single(m => m.Id == member).AccumulatedAmount);

    // ── AC-talad-004 · BR-talad-039@v1 — paid once; the next sale is a new cart ─────────────────────────────

    [Fact, Trait("ac", "AC-talad-004"), Trait("ac", "AC-talad-037")]
    public async Task Somchai_pays_90_and_gets_a_new_empty_cart()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 2);

        var sale = await Paid(await Pay(somchai, cart));
        var next = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!;

        Assert.Equal((90m, "สมชาย", "PAID"), (sale.NetTotal, sale.SellerName, sale.Status));
        Assert.Equal(Sale.ReceiptNumber(cart, sale.PaidAt), sale.ReceiptNo);
        Assert.NotEqual(cart, next.Id);
        Assert.Empty(next.Lines);
        Assert.Equal(CartStatus.Paid, Db(db => db.Carts.AsNoTracking().Single(c => c.Id == cart).Status));
    }

    [Fact, Trait("ac", "AC-talad-038")]
    public async Task The_owner_selling_from_their_own_cart_is_the_seller()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var cart = await Put(owner, orange, 1);

        var sale = await Paid(await Pay(owner, cart));

        Assert.Equal(("เจ้าของร้าน", 45m), (sale.SellerName, sale.NetTotal));
        Assert.Equal(Db(db => db.UserAccounts.Single(a => a.Username == "owner").Id), Sales().Single().SellerId);
    }

    [Fact, Trait("ac", "AC-talad-039")]
    public async Task The_seller_is_whoever_opened_the_cart_not_who_used_the_till_before()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var mangosteen = Product("มังคุด แพ็ก", 120m, 10);
        var somchai = await Somchai();
        var somchaisCart = await Put(somchai, orange, 2);
        var manee = await Manee();
        var maneesCart = await Put(manee, mangosteen, 1);

        var maneesBill = await Paid(await Pay(manee, maneesCart));
        var somchaisBill = await Paid(await Pay(somchai, somchaisCart));

        Assert.Equal([("มานี", 120m), ("สมชาย", 90m)], new[] { maneesBill, somchaisBill }.Select(s => (s.SellerName, s.NetTotal)));
    }

    [Fact]
    public async Task Someone_elses_cart_cannot_be_paid_and_nothing_happens()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 2);
        var manee = await Manee();

        var refused = await Refused(await Pay(manee, cart), HttpStatusCode.NotFound);

        Assert.Equal("CART_NOT_FOUND", refused.Code);
        Assert.Empty(Sales());
        Assert.Equal(10, Stock(orange));
    }

    // ── BR-talad-003@v1 — the member accumulates what was really paid ────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-014")]
    public async Task Somying_accumulates_85_50_not_100()
    {
        var owner = await Owner();
        var item = Product("ของ 100 บาท", 100m, 10);
        var somchai = await Somchai();
        var somying = Member("สมหญิง ใจดี", "0811111111", 1000m);
        await Promotion(owner, Percent("ลด 10%", item, 10));
        await MemberRate(owner, 5);
        var cart = await Put(somchai, item, 1);
        await Bind(somchai, somying);

        var sale = await Paid(await Pay(somchai, cart));

        Assert.Equal((85.50m, 1085.50m), (sale.NetTotal, Accumulated(somying)));
        Assert.Equal(somying, Sales().Single().MemberId);
    }

    [Fact, Trait("ac", "AC-talad-015")]
    public async Task A_bill_bound_to_nobody_adds_to_nobody()
    {
        await Owner();
        var item = Product("ของ 100 บาท", 100m, 10);
        var somchai = await Somchai();
        var somying = Member("สมหญิง ใจดี", "0811111111", 1000m);
        var cart = await Put(somchai, item, 1);

        await Paid(await Pay(somchai, cart));

        Assert.Equal(1000m, Accumulated(somying));
        Assert.Null(Sales().Single().MemberId);
    }

    [Fact, Trait("ac", "AC-talad-092")]
    public async Task A_100_percent_member_discount_makes_a_normal_bill_of_0_00()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        await MemberRate(owner, 100);
        var cart = await Put(somchai, orange, 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));

        var sale = await Paid(await Pay(somchai, cart));

        Assert.Equal((45m, 0m, "PAID"), (sale.MemberDiscount, sale.NetTotal, sale.Status));
        Assert.Equal(9, Stock(orange));
    }

    // ── BR-talad-038@v1 — priced as it is when paid ──────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-023")]
    public async Task The_price_changed_while_the_cart_was_open_is_the_one_paid_and_the_bill_keeps_its_version()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 2);
        await owner.PostAsJsonAsync($"/api/products/{orange}/prices", new { price = 50, source = "STOCK_SCREEN" });
        var fifty = Db(db => db.Products.AsNoTracking().Single(p => p.Id == orange).CurrentPriceVersionId);

        var sale = await Paid(await Pay(somchai, cart));
        await owner.PostAsJsonAsync($"/api/products/{orange}/prices", new { price = 55, source = "STOCK_SCREEN" });

        Assert.Equal((100m, 50m), (sale.NetTotal, sale.Lines.Single().UnitPrice));
        var line = Sales().Single().Lines.Single();
        Assert.Equal((50m, fifty), (line.UnitPrice, (int?)line.PriceVersionId)); // BR-talad-035 · 036 — 55 does not reach it
    }

    [Fact, Trait("ac", "AC-talad-024")]
    public async Task The_member_rate_changed_while_the_cart_was_open_is_the_one_paid_and_its_version_kept()
    {
        var owner = await Owner();
        var item = Product("ของ 100 บาท", 100m, 10);
        var somchai = await Somchai();
        await MemberRate(owner, 5);
        var cart = await Put(somchai, item, 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));
        await MemberRate(owner, 10);
        var ten = Db(db => db.MemberDiscountVersions.AsNoTracking().OrderByDescending(v => v.Id).First().Id);

        var sale = await Paid(await Pay(somchai, cart));

        Assert.Equal((10, 90m), (sale.MemberRate, sale.NetTotal));
        Assert.Equal(ten, Sales().Single().MemberDiscountVersionId);
    }

    [Theory]
    [InlineData("AC-talad-025", "2026-09-01", "2026-09-23", 90)]
    [InlineData("AC-talad-026", "2026-09-24", null, 81)]
    public async Task A_promotion_counts_if_it_is_in_force_at_00_01_when_paid(string ac, string start, string? end, int net)
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", orange, 10, start, end));
        var cart = await Put(somchai, orange, 2);

        _clock.Now = Thai(2026, 9, 24, 0, 1);
        var sale = await Paid(await Pay(somchai, cart));

        Assert.True(net == sale.NetTotal, $"{ac}: paid {sale.NetTotal}");
    }

    // ── BR-talad-039@v1 — one cart, one bill, whatever is pressed or resent ────────────────────────────────

    [Theory, Trait("ac", "AC-talad-027"), Trait("ac", "AC-talad-028")]
    [InlineData("AC-talad-027")]
    [InlineData("AC-talad-028")]
    public async Task Paying_the_same_cart_again_makes_no_second_bill_and_moves_nothing_twice(string _)
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var somying = Member("สมหญิง ใจดี", "0811111111", 1000m);
        await MemberRate(owner, 5);
        var cart = await Put(somchai, orange, 2);
        await Bind(somchai, somying);

        var first = await Paid(await Pay(somchai, cart));
        var again = await Refused(await Pay(somchai, cart));

        Assert.Equal(("CART_NOT_OPEN", "ตะกร้านี้ชำระเงินไปแล้ว"), (again.Code, again.Message));
        Assert.Equal(85.50m, first.NetTotal);
        Assert.Single(Sales());
        Assert.Equal((8, 1085.50m), (Stock(orange), Accumulated(somying)));
    }

    [Fact, Trait("ac", "AC-talad-028")]
    public async Task A_resend_after_the_shelf_emptied_or_the_product_was_discontinued_still_says_already_paid()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 2);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 2);
        await Paid(await Pay(somchai, cart));

        var emptied = await Refused(await Pay(somchai, cart));
        await owner.PostAsync($"/api/products/{orange}/discontinue", null);
        var discontinued = await Refused(await Pay(somchai, cart));

        Assert.Equal(["ตะกร้านี้ชำระเงินไปแล้ว", "ตะกร้านี้ชำระเงินไปแล้ว"], new[] { emptied.Message, discontinued.Message });
        Assert.Single(Sales());
    }

    // ── BR-talad-007@v1 — the stock leaves with the bill, free pieces too ─────────────────────────────────

    [Fact, Trait("ac", "AC-talad-067")]
    public async Task A_sale_takes_its_pieces_from_the_stock()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var mangosteen = Product("มังคุด แพ็ก", 120m, 5);
        var somchai = await Somchai();
        await Put(somchai, orange, 2);
        var cart = await Put(somchai, mangosteen, 1);

        await Paid(await Pay(somchai, cart));

        Assert.Equal((8, 4), (Stock(orange), Stock(mangosteen)));
    }

    [Fact, Trait("ac", "AC-talad-068")]
    public async Task A_free_gift_leaves_the_stock_too_and_the_line_says_it_was_free()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var mangosteen = Product("มังคุด แพ็ก", 120m, 5);
        var somchai = await Somchai();
        await Promotion(owner, new { name = "ซื้อ ส้ม 2 แถม มังคุด 1", type = "BUY_X_GET_Y", productA = orange, qtyA = 2, freeProduct = mangosteen, freeQty = 1, startDate = "2026-09-01" });
        await Put(somchai, orange, 2);
        var cart = await Put(somchai, mangosteen, 1);

        var sale = await Paid(await Pay(somchai, cart));

        Assert.Equal((8, 4), (Stock(orange), Stock(mangosteen)));
        Assert.Equal((90m, 120m), (sale.NetTotal, sale.PromoDiscountTotal));
        var gift = Sales().Single().Lines.Single(l => l.ProductId == mangosteen);
        Assert.Equal((1, 1, 120m, 0m), (gift.Qty, gift.FreeQty, gift.PromoDiscount, gift.LineNet));
        Assert.NotNull(gift.PromotionVersionId);
    }

    [Fact, Trait("ac", "AC-talad-070")]
    public async Task Not_enough_left_when_paid_is_refused_with_the_rule_sentence_and_nothing_moves()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 2);
        var somchai = await Somchai();
        var somchaisCart = await Put(somchai, orange, 2);
        var manee = await Manee();
        await Paid(await Pay(manee, await Put(manee, orange, 1)));

        var refused = await Refused(await Pay(somchai, somchaisCart));

        Assert.Equal(("INSUFFICIENT_STOCK", "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 1)"), (refused.Code, refused.Message));
        Assert.Single(Sales());
        Assert.Equal(1, Stock(orange));
        Assert.Equal(CartStatus.Open, Db(db => db.Carts.AsNoTracking().Single(c => c.Id == somchaisCart).Status));
    }

    // ── BR-talad-037@v1 · the rest of what stops a payment ─────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-087")]
    public async Task A_product_discontinued_while_in_the_cart_stops_the_payment()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var cart = await Put(somchai, orange, 2);
        await owner.PostAsync($"/api/products/{orange}/discontinue", null);

        var refused = await Refused(await Pay(somchai, cart));

        Assert.Equal(("PRODUCT_DISCONTINUED", "ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า"), (refused.Code, refused.Message));
        Assert.Empty(Sales());
        Assert.Equal(10, Stock(orange));
    }

    [Fact]
    public async Task An_empty_cart_or_a_member_hidden_since_binding_is_not_paid()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var emptyCart = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!.Id;
        var empty = await Refused(await Pay(somchai, emptyCart));

        var cart = await Put(somchai, orange, 1);
        var somying = Member("สมหญิง ใจดี", "0811111111");
        await Bind(somchai, somying);
        Db(db =>
        {
            db.Members.Single(m => m.Id == somying).Hide(db.UserAccounts.First(a => a.Role == UserRole.Owner), DateTimeOffset.UnixEpoch);
            return 0;
        });
        var hidden = await Refused(await Pay(somchai, cart));

        Assert.Equal(("CART_EMPTY", "ยังไม่มีสินค้าในตะกร้า"), (empty.Code, empty.Message));
        Assert.Equal(("MEMBER_NOT_ACTIVE", "สมาชิก สมหญิง ใจดี ถูกลบแล้ว กรุณาเอาสมาชิกออกจากตะกร้าก่อนชำระเงิน"), (hidden.Code, hidden.Message));
        Assert.Empty(Sales());
        Assert.Equal(10, Stock(orange));
    }

    // ── BR-talad-029@v1 — a tie is asked before anything is paid ─────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-127")]
    public async Task A_tie_is_asked_first_and_the_choice_is_what_the_bill_keeps()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var somchai = await Somchai();
        var tenPercent = await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", orange, 10));
        var threeForTen = await Promotion(owner, new { name = "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%", type = "BUY_X_PERCENT", productA = orange, qtyA = 3, ratePercent = 10, startDate = "2026-09-01" });
        var cart = await Put(somchai, orange, 3);

        var asked = await Refused(await Pay(somchai, cart));
        var sale = await Paid(await Pay(somchai, cart, tenPercent));

        Assert.Equal("PROMOTION_CHOICE_NEEDED", asked.Code);
        Assert.Equal([(tenPercent, 13.50m), (threeForTen, 13.50m)], asked.Choices!.Select(c => (c.Id, c.Discount)));
        Assert.Equal(("ส้มสายน้ำผึ้ง ลด 10%", 13.50m, 121.50m), (sale.Lines.Single().Promotion!.Name, sale.PromoDiscountTotal, sale.NetTotal));
        var kept = Db(db => db.Promotions.AsNoTracking().Single(p => p.Id == tenPercent).CurrentVersionId);
        Assert.Equal(kept, Sales().Single().Lines.Single().PromotionVersionId);
    }

    // ── one reading for API-009 and API-010 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task What_is_paid_is_exactly_what_the_totals_showed()
    {
        var owner = await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        var mangosteen = Product("มังคุด แพ็ก", 120m, 10);
        var longan = Product("ลำไย", 19.75m, 10);
        var somchai = await Somchai();
        await Promotion(owner, new { name = "ลำไย 1 + ส้ม 1 ลด 11%", type = "BUY_AB_PERCENT", productA = longan, qtyA = 1, productB = orange, qtyB = 1, ratePercent = 11, startDate = "2026-09-01" });
        await Promotion(owner, new { name = "ลดทั้งบิล 5%", type = "BILL_PERCENT", ratePercent = 5, minSubtotal = 0, startDate = "2026-09-01" });
        await MemberRate(owner, 7);
        await Put(somchai, longan, 1);
        await Put(somchai, orange, 1);
        var cart = await Put(somchai, mangosteen, 2);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));

        var shown = (await somchai.GetFromJsonAsync<CartPricingView>("/api/cart/pricing"))!;
        var sale = await Paid(await Pay(somchai, cart));

        Assert.Equal(
            (shown.Subtotal, shown.PromoDiscount, shown.BillDiscount, shown.MemberDiscount, shown.Net),
            ((decimal?)sale.Subtotal, (decimal?)sale.PromoDiscountTotal, (decimal?)sale.BillDiscount, (decimal?)sale.MemberDiscount, (decimal?)sale.NetTotal));
        Assert.Equal(
            shown.Lines.Select(l => (l.ProductId, l.UnitPrice, l.Promotion?.Name, l.ItemPromoDiscount, l.LineNet)),
            sale.Lines.Select(l => (l.ProductId, l.UnitPrice, l.Promotion?.Name, (decimal?)l.PromoDiscount, (decimal?)l.LineNet)));
        Assert.Equal("ลดทั้งบิล 5%", sale.BillPromotion!.Name);
        Assert.NotNull(Sales().Single().BillPromotionVersionId);
    }

    // ── the concurrency check the retries stand on ──────────────────────────────────────────────────

    [Fact]
    public async Task A_stock_moved_by_someone_else_between_read_and_save_is_a_conflict_not_an_overwrite()
    {
        await Owner();
        var orange = Product("ส้มสายน้ำผึ้ง", 45m, 10);
        using var first = _app.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<TaladDbContext>();
        var seen = a.Products.Single(p => p.Id == orange);

        Db(db =>
        {
            db.Products.Single(p => p.Id == orange).RemoveSold(3); // another sale lands meanwhile
            return 0;
        });
        seen.RemoveSold(1);

        await Assert.ThrowsAsync<ConcurrentUpdateException>(() => a.SaveChangesAsync());
        Assert.Equal(7, Stock(orange));
    }

    [Fact]
    public async Task A_lost_race_is_run_again_from_a_fresh_read()
    {
        var reset = new CountingReset();
        var calls = 0;

        var result = await Conflicts.RetryAsync(reset, () => ++calls < 3 ? throw new ConcurrentUpdateException("x") : Task.FromResult(calls));

        Assert.Equal((3, 2), (result, reset.Count));
        await Assert.ThrowsAsync<ConcurrentUpdateException>(() => Conflicts.RetryAsync<int>(reset, () => throw new ConcurrentUpdateException("x")));
    }

    private sealed class CountingReset : IUnitOfWork
    {
        public int Count { get; private set; }
        public void Reset() => Count++;
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_checkout_answers_401()
    {
        var response = await _app.CreateClient().PostAsJsonAsync("/api/cart/checkout", new CheckoutRequest(1, []));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
