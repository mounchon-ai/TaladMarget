using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Sales;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Sales;

/// <summary>
/// UC-talad-002 · BR-talad-020@v1 — a cart left open survives sign-out and comes back to the person who
/// opened it, and to nobody else. Walked through the real endpoints: sign in (API-001), sign out
/// (API-002), and the caller's own cart (API-004..006) — the cart is only ever found by the token's owner.
/// </summary>
[Trait("feature", "FE-talad-007")]
public class HeldCartTests : IClassFixture<TaladApiFactory>
{
    private readonly TaladApiFactory _factory;

    public HeldCartTests(TaladApiFactory factory) => _factory = factory;

    private async Task<HttpClient> SignIn(string username, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task SignOut(HttpClient client)
    {
        var response = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>สมชาย · มานี · the owner, and ส้มสายน้ำผึ้ง at ฿45 — somchai's cart holds 2 of it, then he signs out.</summary>
    private async Task<(int Orange, string Tag)> SomchaiLeavesTwoOrangesAndSignsOut()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        _factory.Seed($"somchai-{tag}", "สมชาย", "Somchai#2569");
        _factory.Seed($"manee-{tag}", "มานี", "Manee#2569");
        _factory.Seed($"owner-{tag}", "เจ้าของร้าน", "Owner#2569", UserRole.Owner);

        int orange;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
            var owner = db.UserAccounts.Single(a => a.Username == $"owner-{tag}");
            var product = new Product("ส้มสายน้ำผึ้ง", null, 10, 0, DateTimeOffset.UnixEpoch);
            db.Products.Add(product);
            db.SaveChanges();
            var price = new ProductPriceVersion(product.Id, 45m, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
            db.ProductPriceVersions.Add(price);
            db.SaveChanges();
            product.PointAtPrice(price);
            db.SaveChanges();
            orange = product.Id;
        }

        var somchai = await SignIn($"somchai-{tag}", "Somchai#2569");
        await somchai.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(orange));
        var cart = (await (await somchai.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(2))).Content.ReadFromJsonAsync<CartView>())!;
        Assert.Equal(90m, cart.Subtotal);
        await SignOut(somchai);
        return (orange, tag);
    }

    [Fact, Trait("ac", "AC-talad-020")]
    public async Task Manee_on_the_same_machine_gets_her_own_empty_cart()
    {
        var (_, tag) = await SomchaiLeavesTwoOrangesAndSignsOut();

        var manee = await SignIn($"manee-{tag}", "Manee#2569");
        var cart = (await manee.GetFromJsonAsync<CartView>("/api/cart"))!;

        Assert.Empty(cart.Lines);
        Assert.Equal(0m, cart.Subtotal);
    }

    [Fact, Trait("ac", "AC-talad-021")]
    public async Task Somchai_signs_back_in_to_his_whole_cart_and_can_carry_on()
    {
        var (orange, tag) = await SomchaiLeavesTwoOrangesAndSignsOut();
        var manee = await SignIn($"manee-{tag}", "Manee#2569");
        await manee.GetFromJsonAsync<CartView>("/api/cart");
        await SignOut(manee);

        var somchai = await SignIn($"somchai-{tag}", "Somchai#2569");
        var back = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!;
        var plus = await somchai.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(3));

        var line = Assert.Single(back.Lines);
        Assert.Equal(("ส้มสายน้ำผึ้ง", 2, 90m), (line.Name, line.Qty, back.Subtotal));
        Assert.Equal(HttpStatusCode.OK, plus.StatusCode);
        Assert.Equal(3, (await plus.Content.ReadFromJsonAsync<CartView>())!.Lines.Single().Qty);
    }

    [Fact, Trait("ac", "AC-talad-022")]
    public async Task The_owner_sees_only_their_own_empty_cart_and_cannot_touch_somchais()
    {
        var (orange, tag) = await SomchaiLeavesTwoOrangesAndSignsOut();

        var owner = await SignIn($"owner-{tag}", "Owner#2569");
        var ownerCart = (await owner.GetFromJsonAsync<CartView>("/api/cart"))!;
        var tryPlus = await owner.PatchAsJsonAsync($"/api/cart/lines/{orange}", new SetQtyRequest(5));
        var tryRemove = await owner.DeleteAsync($"/api/cart/lines/{orange}");

        Assert.Empty(ownerCart.Lines);
        Assert.Equal(0m, ownerCart.Subtotal);
        // the owner's calls land on the owner's own cart, which has no such line — somchai's is out of reach
        Assert.Equal(HttpStatusCode.NotFound, tryPlus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tryRemove.StatusCode);

        var somchai = await SignIn($"somchai-{tag}", "Somchai#2569");
        var untouched = (await somchai.GetFromJsonAsync<CartView>("/api/cart"))!;
        Assert.Equal(2, untouched.Lines.Single().Qty);
        Assert.NotEqual(ownerCart.Id, untouched.Id);
    }
}
