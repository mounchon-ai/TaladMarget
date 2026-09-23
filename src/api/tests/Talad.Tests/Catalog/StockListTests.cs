using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Application.Catalog;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Catalog;

/// <summary>
/// UC-talad-019 · API-003 — the owner's stock list (UI-talad-010) reads the same API-003 the sales screen does
/// (FE-talad-005): products still sold, 20 a page, with what is left and the low-stock flag at each product's
/// own threshold (BR-talad-008@v1). The flag moving after a sale (AC-talad-071 · 072) or a stock-in
/// (AC-talad-074) needs checkout and stock adjustment, which do not exist yet; this proves what the owner's
/// list shows for a given stock, which is the half this unit owns.
/// </summary>
[Trait("feature", "FE-talad-017")]
public sealed class StockListTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> Owner()
    {
        _factory.Seed("owner", "เจ้าของร้าน", "Pass#2569", UserRole.Owner);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner", "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private int SeedProduct(string name, int stock, int threshold, string? barcode = null, decimal price = 45m, bool discontinued = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
        var product = new Product(name, barcode, stock, threshold, DateTimeOffset.UnixEpoch);
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

    private static async Task<PagedResult<ProductCard>> List(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<PagedResult<ProductCard>>($"/api/products{query}"))!;

    // ── BR-talad-008@v1 — each product's own threshold ─────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-073")]
    public async Task Same_stock_different_thresholds_orange_is_low_and_mangosteen_is_not()
    {
        var owner = await Owner();
        SeedProduct("ส้มสายน้ำผึ้ง", stock: 3, threshold: 5);
        SeedProduct("มังคุด แพ็ก", stock: 3, threshold: 2);

        var page = await List(owner);

        Assert.Equal(
            [("มังคุด แพ็ก", 3, false), ("ส้มสายน้ำผึ้ง", 3, true)],
            page.Items.Select(c => (c.Name, c.StockQty, c.LowStock)));
    }

    [Theory, Trait("ac", "AC-talad-071"), Trait("ac", "AC-talad-072"), Trait("ac", "AC-talad-074")]
    [InlineData(5, true)]   // AC-071 · at the threshold
    [InlineData(4, true)]   // AC-074 given · below it
    [InlineData(6, false)]  // AC-072 · one above
    [InlineData(14, false)] // AC-074 then · after +10 in
    [InlineData(0, true)]   // nothing left
    public async Task The_owner_sees_the_low_stock_flag_at_and_below_the_threshold_only(int left, bool low)
    {
        var owner = await Owner();
        SeedProduct("ส้มสายน้ำผึ้ง", stock: left, threshold: 5);

        var card = Assert.Single((await List(owner)).Items);

        Assert.Equal((left, low), (card.StockQty, card.LowStock));
    }

    // ── UI-talad-010's columns ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Each_row_carries_name_barcode_current_price_and_what_is_left()
    {
        var owner = await Owner();
        var id = SeedProduct("ทุเรียนหมอนทอง", stock: 12, threshold: 2, barcode: "8850000000017", price: 250m);

        var card = Assert.Single((await List(owner)).Items);

        Assert.Equal((id, "ทุเรียนหมอนทอง", "8850000000017", 250m, 12, false), (card.Id, card.Name, card.Barcode, card.Price, card.StockQty, card.LowStock));
    }

    // ── UC-talad-019 main · alternate · exception flows ──────────────────────────────────────────────

    [Fact]
    public async Task Only_products_still_sold_are_listed()
    {
        var owner = await Owner();
        SeedProduct("ส้มสายน้ำผึ้ง", stock: 3, threshold: 5);
        SeedProduct("ขนมปังเลิกขาย", stock: 0, threshold: 5, discontinued: true);

        var page = await List(owner);

        Assert.Equal(["ส้มสายน้ำผึ้ง"], page.Items.Select(c => c.Name));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Search_by_part_of_the_name_or_the_whole_barcode_and_nothing_found_is_an_empty_page()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 3, threshold: 5, barcode: "8850000000011");
        SeedProduct("มังคุด แพ็ก", stock: 3, threshold: 2);

        var byName = await List(owner, "?search=" + Uri.EscapeDataString("น้ำผึ้ง"));
        var byBarcode = await List(owner, "?search=8850000000011");
        var partOfBarcode = await List(owner, "?search=88500");
        var none = await List(owner, "?search=" + Uri.EscapeDataString("ไม่มีสินค้านี้"));

        Assert.Equal([orange], byName.Items.Select(c => c.Id));
        Assert.Equal([orange], byBarcode.Items.Select(c => c.Id));
        Assert.Empty(partOfBarcode.Items);
        Assert.Equal((0, 0), (none.Items.Count, none.Total));
    }

    [Fact, Trait("nfr", "NFR-talad-006")]
    public async Task Twenty_one_products_page_at_20_by_name_with_the_total()
    {
        var owner = await Owner();
        for (var i = 20; i >= 0; i--) SeedProduct($"สินค้า {i:D2}", stock: 10, threshold: 1);

        var first = await List(owner);
        var second = await List(owner, "?page=2");

        Assert.Equal((20, 21, 20), (first.Items.Count, first.Total, first.PageSize));
        Assert.Equal("สินค้า 00", first.Items[0].Name);
        Assert.Equal(["สินค้า 20"], second.Items.Select(c => c.Name));
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_the_list_answers_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
