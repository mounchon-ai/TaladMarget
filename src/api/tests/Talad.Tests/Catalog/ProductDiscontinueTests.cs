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
/// UC-talad-018 · API-023 — the owner discontinues a product (STM-talad-001 ACTIVE → DISCONTINUED, final). Nothing
/// is deleted, whether or not a bill sold it (BR-talad-037@v1). AC-talad-085's bill half (the 90-baht bill still shows
/// ส้มสายน้ำผึ้ง 2 × 45) and AC-talad-087's checkout half (paying is refused, no bill) need checkout, which is
/// FE-talad-033's; this proves the halves this unit owns: gone from the sales screen and the stock list, every price
/// version kept, and a cart that holds it keeps the line, is refused more of it, and can take it out.
/// </summary>
[Trait("feature", "FE-talad-021")]
public sealed class ProductDiscontinueTests : IDisposable
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

    private Task<HttpClient> Somchai() => SignedInAs("somchai", UserRole.Cashier);

    private int SeedProduct(string name, string? barcode = null, decimal price = 45m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
        var product = new Product(name, barcode, 10, 0, DateTimeOffset.UnixEpoch);
        db.Products.Add(product);
        db.SaveChanges();
        var version = new ProductPriceVersion(product.Id, price, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
        db.ProductPriceVersions.Add(version);
        db.SaveChanges();
        product.PointAtPrice(version);
        db.SaveChanges();
        return product.Id;
    }

    private static Task<HttpResponseMessage> Discontinue(HttpClient client, int id) => client.PostAsync($"/api/products/{id}/discontinue", null);

    private static async Task<PagedResult<ProductCard>> Search(HttpClient client, string term = "") =>
        (await client.GetFromJsonAsync<PagedResult<ProductCard>>($"/api/products?search={Uri.EscapeDataString(term)}"))!;

    private (ProductStatus Status, int? CurrentPriceVersionId, List<ProductPriceVersion> Versions) Stored(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var product = db.Products.AsNoTracking().Single(p => p.Id == productId);
        var versions = db.ProductPriceVersions.AsNoTracking().Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToList();
        return (product.Status, product.CurrentPriceVersionId, versions);
    }

    private static async Task<string> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProductError>())!.Code;

    // ── AC-talad-085 — a product that was sold leaves the sales screen and the stock list, its prices stay ─

    [Fact, Trait("ac", "AC-talad-085")]
    public async Task Discontinuing_the_orange_takes_it_off_the_sales_screen_and_the_stock_list_and_keeps_its_prices()
    {
        var owner = await Owner();
        var somchai = await Somchai();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", "8850000000011");
        var milk = SeedProduct("นมจืด", price: 15m);
        // a second version, so the row an old bill points at is not the one in force
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/products/{orange}/prices", new RepriceRequest(50m, "STOCK_SCREEN"))).StatusCode);
        var before = Stored(orange);

        var response = await Discontinue(owner, orange);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // the sales screen (the seller's search, by name and by barcode) and the stock list (the owner's) no longer find it
        Assert.Empty((await Search(somchai, "ส้มสายน้ำผึ้ง")).Items);
        Assert.Empty((await Search(somchai, "8850000000011")).Items);
        var stock = await Search(owner);
        Assert.Equal([milk], stock.Items.Select(p => p.Id));
        Assert.Equal(1, stock.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/products/{orange}")).StatusCode);
        // not deleted: the product, its pointer and both price versions are as they were — a bill still finds 45
        var after = Stored(orange);
        Assert.Equal(ProductStatus.Discontinued, after.Status);
        Assert.Equal(before.CurrentPriceVersionId, after.CurrentPriceVersionId);
        Assert.Equal(
            before.Versions.Select(v => (v.Id, v.Price, v.PreviousPrice, v.ChangedAt)),
            after.Versions.Select(v => (v.Id, v.Price, v.PreviousPrice, v.ChangedAt)));
        Assert.Equal([45m, 50m], after.Versions.Select(v => v.Price));
    }

    // ── AC-talad-086 — never sold is discontinued the same way, not deleted ──────────────────────────────

    [Fact, Trait("ac", "AC-talad-086")]
    public async Task The_durian_added_by_mistake_is_discontinued_the_same_way_not_deleted()
    {
        var owner = await Owner();
        var somchai = await Somchai();
        var durian = SeedProduct("ทุเรียนหมอนทอง", price: 250m);

        await Discontinue(owner, durian);

        Assert.Empty((await Search(somchai, "ทุเรียนหมอนทอง")).Items);
        Assert.Empty((await Search(owner, "ทุเรียน")).Items);
        var stored = Stored(durian);
        Assert.Equal(ProductStatus.Discontinued, stored.Status);
        Assert.Equal(250m, Assert.Single(stored.Versions).Price);
    }

    // ── AC-talad-087 — the cart half: the line stays, more is refused with the rule's sentence, it can be taken out ─

    [Fact, Trait("ac", "AC-talad-087")]
    public async Task Somchais_cart_keeps_the_orange_is_refused_more_and_can_take_it_out()
    {
        var owner = await Owner();
        var somchai = await Somchai();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        await somchai.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));
        await somchai.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(2));

        await Discontinue(owner, orange);
        var held = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!;
        var more = await somchai.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(3));
        var removed = await somchai.DeleteAsync($"/api/cart/lines/{orange}");

        Assert.Equal(("ส้มสายน้ำผึ้ง", 2), (held.Lines.Single().Name, held.Lines.Single().Qty));
        Assert.Equal(HttpStatusCode.Conflict, more.StatusCode);
        var refusal = (await more.Content.ReadFromJsonAsync<CartError>())!;
        Assert.Equal(("PRODUCT_DISCONTINUED", "ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า"), (refusal.Code, refusal.Message));
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Empty((await removed.Content.ReadFromJsonAsync<CartView>())!.Lines);
    }

    // ── STM-talad-001 — DISCONTINUED is final ───────────────────────────────────────────────────────

    [Fact]
    public async Task Discontinuing_twice_or_an_unknown_product_is_not_found()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        await Discontinue(owner, orange);

        var again = await Discontinue(owner, orange);
        var unknown = await Discontinue(owner, 999999);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (again.StatusCode, unknown.StatusCode));
        Assert.Equal(("PRODUCT_NOT_FOUND", "PRODUCT_NOT_FOUND"), (await Code(again), await Code(unknown)));
        Assert.Equal(ProductStatus.Discontinued, Stored(orange).Status);
    }

    [Fact]
    public void The_domain_has_no_way_back_from_discontinued()
    {
        var owner = new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
        var product = new Product("ส้มสายน้ำผึ้ง", null, 10, 0, DateTimeOffset.UnixEpoch);
        product.Discontinue(owner);

        Assert.Throws<ProductNotActiveException>(() => product.Discontinue(owner));
        Assert.Equal(ProductStatus.Discontinued, product.Status);
    }

    // ── ACL-020 — the owner's alone ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seller_cannot_discontinue_and_the_product_stays_active()
    {
        await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var somchai = await Somchai();

        var response = await Discontinue(somchai, orange);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProductStatus.Active, Stored(orange).Status);
        Assert.Single((await Search(somchai, "ส้มสายน้ำผึ้ง")).Items);
    }

    [Fact]
    public void The_domain_refuses_a_seller_even_without_the_endpoint_policy()
    {
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        var product = new Product("ส้มสายน้ำผึ้ง", null, 10, 0, DateTimeOffset.UnixEpoch);

        Assert.Throws<ProductOwnerOnlyException>(() => product.Discontinue(seller));
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_discontinue_answers_401()
    {
        var response = await Discontinue(_factory.CreateClient(), 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
