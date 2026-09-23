using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Application.Catalog;
using Talad.Application.Promotions;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Promotions;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Promotions;

/// <summary>
/// UC-talad-022 · API-029 — the owner discontinues a promotion (STM-talad-002 ACTIVE → DISCONTINUED, final).
/// Nothing is deleted, whether or not a bill used it (BR-talad-037@v1). AC-talad-064's and AC-talad-088's
/// bill halves (the old bill keeps 9 baht off · a new bill pays 90) need checkout, which does not exist yet;
/// this proves the halves this unit owns: every version is kept, and the promotion leaves the list.
/// </summary>
[Trait("feature", "FE-talad-027")]
public sealed class PromotionDiscontinueTests : IDisposable
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

    private int SeedProduct(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
        var product = new Product(name, null, 10, 0, DateTimeOffset.UnixEpoch);
        db.Products.Add(product);
        db.SaveChanges();
        var price = new ProductPriceVersion(product.Id, 45m, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
        db.ProductPriceVersions.Add(price);
        db.SaveChanges();
        product.PointAtPrice(price);
        db.SaveChanges();
        return product.Id;
    }

    private static object TenPercent(int product, string name = "ส้มสายน้ำผึ้ง ลด 10%", int rate = 10) =>
        new { name, type = "ITEM_PERCENT", productA = product, ratePercent = rate, startDate = "2026-09-01" };

    private static async Task<PromotionView> Created(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PromotionView>())!;
    }

    private static Task<HttpResponseMessage> Discontinue(HttpClient client, int id) => client.PostAsync($"/api/promotions/{id}/discontinue", null);

    private (PromotionStatus Status, int? CurrentVersionId, List<PromotionVersion> Versions) Stored(int promotionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var promotion = db.Promotions.AsNoTracking().Single(p => p.Id == promotionId);
        var versions = db.PromotionVersions.AsNoTracking().Where(v => v.PromotionId == promotionId).OrderBy(v => v.Id).ToList();
        return (promotion.Status, promotion.CurrentVersionId, versions);
    }

    private static async Task<string> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Talad.Api.Promotions.PromotionError>())!.Code;

    // ── AC-talad-064 — discontinued, and every version the bills point at is still there ──────────────

    [Fact, Trait("ac", "AC-talad-064")]
    public async Task Discontinuing_the_10_percent_promotion_keeps_every_version_as_it_was()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var promotion = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange)));
        await Created(await owner.PostAsJsonAsync($"/api/promotions/{promotion.Id}/versions", TenPercent(orange, "ส้มสายน้ำผึ้ง ลด 20%", 20)));
        var before = Stored(promotion.Id);

        var response = await Discontinue(owner, promotion.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = Stored(promotion.Id);
        Assert.Equal(PromotionStatus.Discontinued, after.Status);
        // the pointer and both versions are untouched — a bill paid under either still finds its conditions
        Assert.Equal(before.CurrentVersionId, after.CurrentVersionId);
        Assert.Equal(
            before.Versions.Select(v => (v.Id, v.Name, v.ProductAId, v.RatePercent, v.StartDate, v.CreatedAt)),
            after.Versions.Select(v => (v.Id, v.Name, v.ProductAId, v.RatePercent, v.StartDate, v.CreatedAt)));
        Assert.Equal([10, 20], after.Versions.Select(v => v.RatePercent));
    }

    // ── AC-talad-088 — gone from the management screen ──────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-088")]
    public async Task A_discontinued_promotion_leaves_the_list_and_is_not_found_nor_edited()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var gone = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange)));
        var kept = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange, "ส้มสายน้ำผึ้ง ลด 5%", 5)));

        await Discontinue(owner, gone.Id);
        var list = (await owner.GetFromJsonAsync<PagedResult<PromotionView>>("/api/promotions?search=ส้ม"))!;
        var open = await owner.GetAsync($"/api/promotions/{gone.Id}");
        var edit = await owner.PostAsJsonAsync($"/api/promotions/{gone.Id}/versions", TenPercent(orange, rate: 20));

        Assert.Equal([kept.Id], list.Items.Select(p => p.Id));
        Assert.Equal(1, list.Total);
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (open.StatusCode, edit.StatusCode));
        Assert.Single(Stored(gone.Id).Versions);
    }

    // ── BR-talad-037@v1 — never used in a bill is discontinued the same way, not deleted ────────────────

    [Fact]
    public async Task A_promotion_no_bill_ever_used_is_discontinued_the_same_way_not_deleted()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var unused = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange)));

        await Discontinue(owner, unused.Id);

        var stored = Stored(unused.Id);
        Assert.Equal((PromotionStatus.Discontinued, unused.VersionId), (stored.Status, stored.CurrentVersionId));
        Assert.Single(stored.Versions);
    }

    // ── STM-talad-002 — DISCONTINUED is final ───────────────────────────────────────────────────────

    [Fact]
    public async Task Discontinuing_twice_or_an_unknown_promotion_is_not_found()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var promotion = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange)));
        await Discontinue(owner, promotion.Id);

        var again = await Discontinue(owner, promotion.Id);
        var unknown = await Discontinue(owner, 999999);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (again.StatusCode, unknown.StatusCode));
        Assert.Equal(("PROMOTION_NOT_FOUND", "PROMOTION_NOT_FOUND"), (await Code(again), await Code(unknown)));
        Assert.Equal(PromotionStatus.Discontinued, Stored(promotion.Id).Status);
    }

    [Fact]
    public void The_domain_has_no_way_back_from_discontinued()
    {
        var owner = new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
        var promotion = Promotion.Open(owner, DateTimeOffset.UnixEpoch);
        promotion.Discontinue(owner);

        Assert.Throws<PromotionNotActiveException>(() => promotion.Discontinue(owner));
        Assert.Throws<PromotionNotActiveException>(() => promotion.Draft(null!, owner, DateTimeOffset.UnixEpoch));
    }

    // ── ACL-024 — the owner's alone ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seller_cannot_discontinue_and_the_promotion_stays_active()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var promotion = await Created(await owner.PostAsJsonAsync("/api/promotions", TenPercent(orange)));
        var somchai = await SignedInAs("somchai", UserRole.Cashier);

        var response = await Discontinue(somchai, promotion.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(PromotionStatus.Active, Stored(promotion.Id).Status);
    }

    [Fact]
    public void The_domain_refuses_a_seller_even_without_the_endpoint_policy()
    {
        var owner = new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        var promotion = Promotion.Open(owner, DateTimeOffset.UnixEpoch);

        Assert.Throws<PromotionOwnerOnlyException>(() => promotion.Discontinue(seller));
        Assert.Equal(PromotionStatus.Active, promotion.Status);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_discontinue_answers_401()
    {
        var response = await Discontinue(_factory.CreateClient(), 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
