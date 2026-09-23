using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Catalog;
using Talad.Api.Sales;
using Talad.Application.Catalog;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Catalog;

/// <summary>
/// UC-talad-017 · API-019 · API-022 — the owner changes a price from the stock screen or the sales screen's
/// shortcut (BR-talad-019@v1); every change is a new ENT-002 row carrying the price it replaced, and the row
/// before stays as it was (BR-talad-033@v1). AC-talad-060's bill half (the paid bill still shows 45) needs
/// checkout, which does not exist yet; this proves the half this unit owns — the 45 row is untouched.
/// History is newest first (UC-talad-017 says "เรียงตามเวลา" without a direction — dev's choice, named at 🛑).
/// </summary>
[Trait("feature", "FE-talad-019")]
public sealed class ProductPricingTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SignedInAs(string username, UserRole role)
    {
        _factory.Seed(username, username == "owner" ? "เจ้าของร้าน" : "สมชาย", "Pass#2569", role);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> Owner() => SignedInAs("owner", UserRole.Owner);

    private int SeedProduct(string name, decimal price = 45m, bool discontinued = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
        var product = new Product(name, "8850000000011", 10, 5, DateTimeOffset.UnixEpoch);
        db.Products.Add(product);
        db.SaveChanges();
        var version = new ProductPriceVersion(product.Id, price, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
        db.ProductPriceVersions.Add(version);
        db.SaveChanges();
        product.PointAtPrice(version);
        if (discontinued) product.Discontinue(owner);
        db.SaveChanges();
        return product.Id;
    }

    private static Task<HttpResponseMessage> Reprice(HttpClient client, int id, object body) => client.PostAsJsonAsync($"/api/products/{id}/prices", body);

    private static async Task<ProductDetail> Detail(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDetail>())!;
    }

    private List<ProductPriceVersion> StoredVersions(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().ProductPriceVersions.AsNoTracking()
            .Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToList();
    }

    private static async Task<ProductError> Error(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductError>())!;
    }

    // ── AC-talad-082 · 083 · 084 — every change is a history row ────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-082")]
    public async Task Changing_45_to_50_at_the_stock_screen_makes_one_history_row_by_the_owner()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var before = (await owner.GetFromJsonAsync<ProductDetail>($"/api/products/{orange}"))!;
        var sent = DateTimeOffset.UtcNow;

        var after = await Detail(await Reprice(owner, orange, new { price = 50m, source = "STOCK_SCREEN" }));

        Assert.Empty(before.PriceHistory.Items); // the price the product was created with is not a change
        Assert.Equal(50m, after.Price);
        var change = Assert.Single(after.PriceHistory.Items);
        Assert.Equal((45m, 50m, "STOCK_SCREEN", "เจ้าของร้าน"), (change.PreviousPrice, change.Price, change.Source, change.ChangedByName));
        Assert.InRange(change.ChangedAt, sent.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact, Trait("ac", "AC-talad-082")]
    public void The_change_is_stamped_with_the_time_it_was_made()
    {
        var owner = new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
        var product = new Product("ส้มสายน้ำผึ้ง", null, 10, 5, DateTimeOffset.UnixEpoch);
        product.PointAtPrice(new ProductPriceVersion(0, 45m, null, PriceChangeSource.StockScreen, 0, DateTimeOffset.UnixEpoch));
        var tenOClock = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.FromHours(7)); // 23 ก.ย. 2569 10:00

        var version = product.Reprice(50m, PriceChangeSource.StockScreen, owner, tenOClock);

        Assert.Equal((45m, 50m, tenOClock), (version.PreviousPrice, version.Price, version.ChangedAt));
    }

    [Fact, Trait("ac", "AC-talad-083")]
    public async Task Changing_45_to_48_from_the_sales_screen_is_recorded_the_same_way()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var after = await Detail(await Reprice(owner, orange, new { price = 48m, source = "SALES_SCREEN" }));

        var change = Assert.Single(after.PriceHistory.Items);
        Assert.Equal((45m, 48m, "SALES_SCREEN", "เจ้าของร้าน"), (change.PreviousPrice, change.Price, change.Source, change.ChangedByName));
        Assert.Equal(PriceChangeSource.SalesScreen, StoredVersions(orange)[1].Source);
    }

    [Fact, Trait("ac", "AC-talad-084"), Trait("ac", "AC-talad-060")]
    public async Task Changing_twice_keeps_both_rows_and_the_45_row_exactly_as_it_was()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var original = StoredVersions(orange).Single();

        await Reprice(owner, orange, new { price = 50m, source = "STOCK_SCREEN" });
        var after = await Detail(await Reprice(owner, orange, new { price = 55m, source = "STOCK_SCREEN" }));

        Assert.Equal([(50m, 55m), (45m, 50m)], after.PriceHistory.Items.Select(h => (h.PreviousPrice, h.Price)));
        Assert.Equal(2, after.PriceHistory.Total);
        var stored = StoredVersions(orange);
        Assert.Equal([45m, 50m, 55m], stored.Select(v => v.Price));
        // AC-talad-060 — the row a paid bill points at is not touched by later changes
        var kept = stored[0];
        Assert.Equal((original.Id, original.Price, original.PreviousPrice, original.Source, original.ChangedById, original.ChangedAt),
            (kept.Id, kept.Price, kept.PreviousPrice, kept.Source, kept.ChangedById, kept.ChangedAt));
    }

    [Fact, Trait("nfr", "NFR-talad-006")]
    public async Task A_long_history_pages_at_20_on_its_own_parameter()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        for (var i = 1; i <= 21; i++) await Reprice(owner, orange, new { price = 45m + i, source = "STOCK_SCREEN" });

        var first = (await owner.GetFromJsonAsync<ProductDetail>($"/api/products/{orange}"))!;
        var second = (await owner.GetFromJsonAsync<ProductDetail>($"/api/products/{orange}?historyPage=2"))!;

        Assert.Equal((20, 21, 20, 66m), (first.PriceHistory.Items.Count, first.PriceHistory.Total, first.PriceHistory.PageSize, first.PriceHistory.Items[0].Price));
        Assert.Equal([(45m, 46m)], second.PriceHistory.Items.Select(h => (h.PreviousPrice, h.Price)));
        Assert.Equal(66m, second.Price);
    }

    // ── AC-talad-048 — the next cart takes the new price ─────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-048")]
    public async Task After_the_owner_changes_45_to_50_somchai_picks_the_orange_at_50()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var somchai = await SignedInAs("somchai", UserRole.Cashier);

        await Reprice(owner, orange, new { price = 50m, source = "SALES_SCREEN" });
        var cart = (await (await somchai.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange))).Content.ReadFromJsonAsync<CartView>())!;

        Assert.Equal((orange, 50m, 1, 50m), (cart.Lines.Single().ProductId, cart.Lines.Single().Price, cart.Lines.Single().Qty, cart.Subtotal));
    }

    // ── UI-talad-012 summary ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_detail_carries_the_summary_the_owner_sees()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var detail = (await owner.GetFromJsonAsync<ProductDetail>($"/api/products/{orange}"))!;

        Assert.Equal((orange, "ส้มสายน้ำผึ้ง", "8850000000011", 10, 5, false, 45m), (detail.Id, detail.Name, detail.Barcode, detail.StockQty, detail.LowStockThreshold, detail.LowStock, detail.Price));
    }

    // ── refusals — nothing is saved ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, PriceMessages.Invalid)]
    [InlineData(-1, PriceMessages.Invalid)]
    [InlineData(45.555, PriceMessages.Invalid)]
    [InlineData(10_000_000_000, PriceMessages.TooHigh)]
    public async Task A_price_not_above_0_finer_than_satang_or_beyond_the_column_is_refused_under_price(double price, string message)
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var error = await Error(await Reprice(owner, orange, new { price = (decimal)price, source = "STOCK_SCREEN" }), HttpStatusCode.BadRequest);

        Assert.Equal(("PRICE_INVALID", "price", message), (error.Code, error.Errors.Single().Field, error.Errors.Single().Message));
        Assert.Single(StoredVersions(orange));
    }

    [Fact]
    public async Task No_price_or_no_source_is_refused_under_its_field()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var noPrice = await Error(await Reprice(owner, orange, new { source = "STOCK_SCREEN" }), HttpStatusCode.BadRequest);
        var noSource = await Error(await Reprice(owner, orange, new { price = 50m, source = "CHAT" }), HttpStatusCode.BadRequest);

        Assert.Equal(("price", PriceMessages.Invalid), (noPrice.Errors.Single().Field, noPrice.Errors.Single().Message));
        Assert.Equal(("source", PriceMessages.SourceRequired), (noSource.Errors.Single().Field, noSource.Errors.Single().Message));
        Assert.Single(StoredVersions(orange));
    }

    [Fact]
    public async Task An_unknown_or_discontinued_product_is_not_found_for_both_calls()
    {
        var owner = await Owner();
        var gone = SeedProduct("ขนมปังเลิกขาย", discontinued: true);

        var open = await Error(await owner.GetAsync($"/api/products/{gone}"), HttpStatusCode.NotFound);
        var change = await Error(await Reprice(owner, gone, new { price = 50m, source = "STOCK_SCREEN" }), HttpStatusCode.NotFound);
        var unknown = await Error(await owner.GetAsync("/api/products/999999"), HttpStatusCode.NotFound);

        Assert.Equal(["PRODUCT_NOT_FOUND", "PRODUCT_NOT_FOUND", "PRODUCT_NOT_FOUND"], new[] { open.Code, change.Code, unknown.Code });
        Assert.Single(StoredVersions(gone));
    }

    // ── ACL-019 — the owner's alone ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seller_can_neither_open_the_detail_nor_change_a_price_but_still_searches_products()
    {
        await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var somchai = await SignedInAs("somchai", UserRole.Cashier);

        var open = await somchai.GetAsync($"/api/products/{orange}");
        var change = await Reprice(somchai, orange, new { price = 50m, source = "SALES_SCREEN" });
        var search = await somchai.GetAsync("/api/products");

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.OK), (open.StatusCode, change.StatusCode, search.StatusCode));
        Assert.Single(StoredVersions(orange));
    }

    [Fact]
    public void The_domain_refuses_a_seller_and_a_discontinued_product_even_without_the_endpoint()
    {
        var owner = new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        var product = new Product("ส้มสายน้ำผึ้ง", null, 10, 5, DateTimeOffset.UnixEpoch);
        product.PointAtPrice(new ProductPriceVersion(0, 45m, null, PriceChangeSource.StockScreen, 0, DateTimeOffset.UnixEpoch));

        Assert.Throws<PriceOwnerOnlyException>(() => product.Reprice(50m, PriceChangeSource.SalesScreen, seller, DateTimeOffset.UnixEpoch));
        product.Discontinue(owner);
        Assert.Throws<ProductNotActiveException>(() => product.Reprice(50m, PriceChangeSource.StockScreen, owner, DateTimeOffset.UnixEpoch));
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_both_calls_answer_401()
    {
        var anonymous = _factory.CreateClient();

        var open = await anonymous.GetAsync("/api/products/1");
        var change = await Reprice(anonymous, 1, new { price = 50m, source = "STOCK_SCREEN" });

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (open.StatusCode, change.StatusCode));
    }
}
