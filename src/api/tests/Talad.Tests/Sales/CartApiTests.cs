using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Sales;
using Talad.Application.Catalog;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Sales;

[Trait("feature", "FE-talad-005")]
public class CartApiTests : IClassFixture<TaladApiFactory>
{
    private readonly TaladApiFactory _factory;
    private readonly HttpClient _client;

    public CartApiTests(TaladApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── fixtures: every test gets its own cashier and its own products, so carts never meet ──────────

    private async Task<HttpClient> SignedInAs(string username)
    {
        _factory.Seed(username, username, "Pass#2569");
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private int SeedProduct(string name, decimal price, int stock, int threshold = 0, string? barcode = null, bool discontinued = false, string? image = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.FirstOrDefault(a => a.Role == UserRole.Owner);
        if (owner is null)
        {
            owner = new UserAccount($"owner-{Guid.NewGuid():N}", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
            owner.SetPasswordHash("unused");
            db.UserAccounts.Add(owner);
            db.SaveChanges();
        }
        var product = new Product(name, barcode, stock, threshold, DateTimeOffset.UnixEpoch, image);
        db.Products.Add(product);
        db.SaveChanges();
        var version = new ProductPriceVersion(product.Id, price, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
        db.ProductPriceVersions.Add(version);
        db.SaveChanges();
        product.PointAtPrice(version);
        if (discontinued) product.Discontinue();
        db.SaveChanges();
        return product.Id;
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    // ── AC-talad-001 · 002 · 003 ────────────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-001")]
    public async Task Pick_orange_then_plus_twice_makes_3_and_135()
    {
        var cashier = await SignedInAs("somchai-001");
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", 45m, stock: 10);

        await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));
        await cashier.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(2));
        var response = await cashier.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(3));

        var cart = (await response.Content.ReadFromJsonAsync<CartView>())!;
        var line = Assert.Single(cart.Lines);
        Assert.Equal(("ส้มสายน้ำผึ้ง", 3), (line.Name, line.Qty));
        Assert.Equal(135m, cart.Subtotal);
    }

    [Fact, Trait("ac", "AC-talad-002")]
    public async Task Minus_once_from_3_makes_2_and_90()
    {
        var cashier = await SignedInAs("somchai-002");
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", 45m, stock: 10);
        for (var i = 0; i < 3; i++) await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));

        var response = await cashier.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(2));

        var cart = (await response.Content.ReadFromJsonAsync<CartView>())!;
        Assert.Equal(2, cart.Lines.Single().Qty);
        Assert.Equal(90m, cart.Subtotal);
    }

    [Fact, Trait("ac", "AC-talad-003")]
    public async Task Removing_the_orange_leaves_the_mangosteen_and_120()
    {
        var cashier = await SignedInAs("somchai-003");
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", 45m, stock: 10);
        var mangosteen = SeedProduct("มังคุด แพ็ก", 120m, stock: 10);
        await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));
        await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(mangosteen));

        var response = await cashier.DeleteAsync($"/api/cart/lines/{orange}");

        var cart = (await response.Content.ReadFromJsonAsync<CartView>())!;
        Assert.Equal(["มังคุด แพ็ก"], cart.Lines.Select(l => l.Name));
        Assert.Equal(120m, cart.Subtotal);
    }

    // ── BR-talad-007@v1 · AC-talad-069 · 110 · 114 ──────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-069")]
    public async Task Plus_beyond_stock_answers_the_rule_sentence_and_keeps_2()
    {
        var cashier = await SignedInAs("somchai-069");
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", 45m, stock: 2);
        await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));
        await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));

        var response = await cashier.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(3));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<CartError>())!;
        Assert.Equal(("INSUFFICIENT_STOCK", "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)"), (error.Code, error.Message));
        var cart = (await cashier.GetFromJsonAsync<CartView>("/api/cart"))!;
        Assert.Equal(2, cart.Lines.Single().Qty);
    }

    [Theory, Trait("ac", "AC-talad-110"), Trait("ac", "AC-talad-114")]
    [InlineData("มังคุด แพ็ก")] // AC-talad-110
    [InlineData("ลำไย")]         // AC-talad-114
    public async Task A_gift_with_nothing_left_does_not_go_in(string gift)
    {
        var cashier = await SignedInAs($"somchai-gift-{gift.Length}");
        var giftId = SeedProduct(gift, 20m, stock: 0);

        var response = await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(giftId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"{gift} คงเหลือไม่พอ (เหลือ 0)", (await response.Content.ReadFromJsonAsync<CartError>())!.Message);
        Assert.Empty((await cashier.GetFromJsonAsync<CartView>("/api/cart"))!.Lines);
    }

    // ── BR-talad-020@v1 — a cart is its opener's alone ──────────────────────────────────────────────

    [Fact]
    public async Task Another_cashier_gets_their_own_empty_cart()
    {
        var somchai = await SignedInAs("somchai-own");
        var manee = await SignedInAs("manee-own");
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", 45m, stock: 10);
        await somchai.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));

        var maneeCart = (await manee.GetFromJsonAsync<CartView>("/api/cart"))!;
        var somchaiCart = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!;

        Assert.Empty(maneeCart.Lines);
        Assert.Equal(0m, maneeCart.Subtotal);
        Assert.NotEqual(somchaiCart.Id, maneeCart.Id);
        Assert.Single(somchaiCart.Lines);
    }

    [Fact]
    public async Task The_same_open_cart_comes_back_on_every_call()
    {
        var cashier = await SignedInAs("somchai-same");

        var first = (await cashier.GetFromJsonAsync<CartView>("/api/cart"))!;
        var second = (await cashier.GetFromJsonAsync<CartView>("/api/cart"))!;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Open", second.Status);
    }

    // ── API-003 · BR-talad-008@v1 · BR-talad-037@v1 ────────────────────────────────────────────────

    [Theory, Trait("ac", "AC-talad-071"), Trait("ac", "AC-talad-072")]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task Product_cards_carry_the_low_stock_flag(int left, bool low)
    {
        var cashier = await SignedInAs($"somchai-low-{left}");
        var name = Unique("ส้มสายน้ำผึ้ง");
        SeedProduct(name, 45m, stock: left, threshold: 5);

        var page = (await cashier.GetFromJsonAsync<PagedResult<ProductCard>>($"/api/products?search={Uri.EscapeDataString(name)}"))!;

        var card = Assert.Single(page.Items);
        Assert.Equal((45m, left, low), (card.Price, card.StockQty, card.LowStock));
    }

    [Fact]
    public async Task Search_finds_part_of_a_name_or_a_whole_barcode_and_never_a_discontinued_product()
    {
        var cashier = await SignedInAs("somchai-search");
        var name = Unique("ทุเรียนหมอนทอง");
        var sold = SeedProduct(name, 250m, stock: 3, barcode: "8850000000017");
        SeedProduct(name + " เก่า", 250m, stock: 3, discontinued: true);

        var byName = (await cashier.GetFromJsonAsync<PagedResult<ProductCard>>($"/api/products?search={Uri.EscapeDataString(name[..6])}"))!;
        var byBarcode = (await cashier.GetFromJsonAsync<PagedResult<ProductCard>>("/api/products?search=8850000000017"))!;

        Assert.Contains(byName.Items, c => c.Id == sold);
        Assert.DoesNotContain(byName.Items, c => c.Name.EndsWith("เก่า"));
        Assert.Equal([sold], byBarcode.Items.Select(c => c.Id));
        Assert.Equal(20, byName.PageSize);
    }

    [Fact]
    public async Task A_discontinued_product_cannot_be_put_in_the_cart()
    {
        var cashier = await SignedInAs("somchai-gone");
        var gone = SeedProduct("ส้มเก่า", 45m, stock: 10, discontinued: true);

        var response = await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(gone));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── API-043 · DEC-002 ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_product_image_is_served_from_the_image_folder()
    {
        var cashier = await SignedInAs("somchai-image");
        await File.WriteAllBytesAsync(Path.Combine(_factory.ImageRoot, "orange.png"), [0x89, 0x50, 0x4E, 0x47]);
        var withImage = SeedProduct("ส้มมีรูป", 45m, stock: 1, image: "orange.png");
        var escaping = SeedProduct("ส้มแอบ", 45m, stock: 1, image: "../secret.png");

        var ok = await cashier.GetAsync($"/api/products/{withImage}/image");
        var refused = await cashier.GetAsync($"/api/products/{escaping}/image");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("image/png", ok.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    // ── NFR-talad-005 ───────────────────────────────────────────────────────────────────────────────

    [Theory, Trait("nfr", "NFR-talad-005")]
    [InlineData("/api/cart")]
    [InlineData("/api/products")]
    public async Task Without_a_token_the_cart_and_products_answer_401(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
