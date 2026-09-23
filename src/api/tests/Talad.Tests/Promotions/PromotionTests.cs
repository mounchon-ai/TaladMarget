using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Promotions;
using Talad.Application.Catalog;
using Talad.Application.Promotions;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Promotions;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Promotions;

/// <summary>
/// UC-talad-021 · API-025..028 — the owner creates and edits promotions in six forms; every edit is a new
/// version and the old one stays as it was (BR-talad-036@v1). AC-talad-062's bill half (the old bill keeps
/// 9 baht off) needs checkout, which does not exist yet; this proves the half this unit owns.
/// No discount is computed here — that is FE-talad-031's.
/// </summary>
[Trait("feature", "FE-talad-025")]
public sealed class PromotionTests : IDisposable
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

    private int SeedProduct(string name, bool discontinued = false)
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
        if (discontinued) product.Discontinue();
        db.SaveChanges();
        return product.Id;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, object body) => client.PostAsJsonAsync("/api/promotions", body);

    private static async Task<PromotionView> Created(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PromotionView>())!;
    }

    private static async Task<(string Field, string Message)[]> Errors(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PromotionError>())!.Errors.Select(e => (e.Field, e.Message)).ToArray();
    }

    private List<PromotionVersion> StoredVersions(int promotionId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().PromotionVersions.Where(v => v.PromotionId == promotionId).OrderBy(v => v.Id).ToList();
    }

    private void Discontinue(int promotionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var promotion = db.Promotions.Single(p => p.Id == promotionId);
        // discontinuing is FE-talad-027's — set the column as it would leave it
        db.Entry(promotion).Property(nameof(Promotion.Status)).CurrentValue = PromotionStatus.Discontinued;
        db.SaveChanges();
    }

    // ── AC-talad-062 — an edit is a new version; the old one stays ──────────────────────────────────

    [Fact, Trait("ac", "AC-talad-062")]
    public async Task Changing_10_percent_to_20_percent_puts_a_new_version_in_force_and_leaves_the_10_percent_one()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var first = await Created(await Create(owner, new { name = "ส้มสายน้ำผึ้ง ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" }));

        var revised = await Created(await owner.PostAsJsonAsync($"/api/promotions/{first.Id}/versions",
            new { name = "ส้มสายน้ำผึ้ง ลด 20%", type = "ITEM_PERCENT", productA = orange, ratePercent = 20, startDate = "2026-09-01" }));
        var current = (await owner.GetFromJsonAsync<PromotionView>($"/api/promotions/{first.Id}"))!;

        Assert.Equal((first.Id, 20, "ส้มสายน้ำผึ้ง ลด 20%"), (current.Id, current.RatePercent, current.Name));
        Assert.NotEqual(first.VersionId, revised.VersionId);
        Assert.Equal(revised.VersionId, current.VersionId);
        var versions = StoredVersions(first.Id);
        Assert.Equal([10, 20], versions.Select(v => v.RatePercent));
        var old = versions[0];
        Assert.Equal((first.VersionId, "ส้มสายน้ำผึ้ง ลด 10%", orange, new DateOnly(2026, 9, 1)), (old.Id, old.Name, old.ProductAId, old.StartDate));
    }

    // ── the six forms (BR-talad-009 · 011..014) ─────────────────────────────────────────────────────

    public static TheoryData<string, object> EveryForm() => new()
    {
        { "ITEM_PERCENT", new { productA = 1, ratePercent = 10 } },
        { "BILL_PERCENT", new { ratePercent = 5, minSubtotal = 500.00m } },
        { "BUY_X_GET_Y", new { productA = 1, qtyA = 2, freeProduct = 1, freeQty = 1 } },
        { "BUY_AB_GET_Y", new { productA = 1, qtyA = 2, productB = 2, qtyB = 1, freeProduct = 2, freeQty = 1 } },
        { "BUY_AB_PERCENT", new { productA = 1, qtyA = 1, productB = 2, qtyB = 1, ratePercent = 15 } },
        { "BUY_X_PERCENT", new { productA = 1, qtyA = 3, ratePercent = 20 } },
    };

    [Theory, MemberData(nameof(EveryForm))]
    public async Task Each_form_is_created_with_the_fields_it_needs(string type, object fields)
    {
        var owner = await Owner();
        SeedProduct("ส้มสายน้ำผึ้ง");
        SeedProduct("มังคุด แพ็ก");
        var body = Merge(fields, new { name = $"โปร {type}", type, startDate = "2026-09-01" });

        var view = await Created(await Create(owner, body));

        Assert.Equal((type, "Active", "2026-09-01", (string?)null), (view.Type, view.Status, view.StartDate, view.EndDate));
    }

    public static TheoryData<string, object, string> MissingField() => new()
    {
        { "ITEM_PERCENT", new { ratePercent = 10 }, "productA" },
        { "BILL_PERCENT", new { ratePercent = 5 }, "minSubtotal" },
        { "BUY_X_GET_Y", new { productA = 1, qtyA = 2, freeQty = 1 }, "freeProduct" },
        { "BUY_AB_GET_Y", new { productA = 1, qtyA = 2, productB = 2, freeProduct = 2, freeQty = 1 }, "qtyB" },
        { "BUY_AB_PERCENT", new { productA = 1, qtyA = 1, qtyB = 1, ratePercent = 15 }, "productB" },
        { "BUY_X_PERCENT", new { productA = 1, ratePercent = 20 }, "qtyA" },
    };

    [Theory, MemberData(nameof(MissingField))]
    public async Task A_field_the_form_needs_left_empty_is_answered_under_that_field(string type, object fields, string missing)
    {
        var owner = await Owner();
        SeedProduct("ส้มสายน้ำผึ้ง");
        SeedProduct("มังคุด แพ็ก");

        var response = await Create(owner, Merge(fields, new { name = "โปร", type, startDate = "2026-09-01" }));

        Assert.Equal([(missing, "กรุณากรอกช่องนี้")], await Errors(response));
    }

    [Fact]
    public async Task Fields_the_form_does_not_use_are_cleared_not_kept()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var view = await Created(await Create(owner, new
        {
            name = "ส้ม ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01",
            qtyA = 3, productB = orange, qtyB = 1, freeProduct = orange, freeQty = 1, minSubtotal = 100m,
        }));

        Assert.Null(view.QtyA);
        Assert.Null(view.ProductB);
        Assert.Null(view.FreeProduct);
        Assert.Null(view.MinSubtotal);
        var stored = StoredVersions(view.Id).Single();
        Assert.Equal(((int?)null, (int?)null, (int?)null, (decimal?)null), (stored.QtyA, stored.ProductBId, stored.FreeProductId, stored.MinSubtotal));
    }

    [Fact]
    public async Task What_the_design_allows_is_allowed_0_percent_a_start_in_the_past_the_free_item_the_same_as_a()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var zero = await Create(owner, new { name = "ลด 0%", type = "ITEM_PERCENT", productA = orange, ratePercent = 0, startDate = "2020-01-01" });
        var sameFree = await Create(owner, new { name = "ซื้อ 2 แถม 1", type = "BUY_X_GET_Y", productA = orange, qtyA = 2, freeProduct = orange, freeQty = 1, startDate = "2026-09-01", endDate = "2026-09-01" });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (zero.StatusCode, sameFree.StatusCode));
    }

    // ── UI-talad-016 state "error" — wrong values under their fields, nothing saved ─────────────────

    [Theory]
    [InlineData("ratePercent", 101, "ส่วนลดต้องเป็นจำนวนเต็ม 0–100")]
    [InlineData("ratePercent", -1, "ส่วนลดต้องเป็นจำนวนเต็ม 0–100")]
    [InlineData("ratePercent", 5.5, "ส่วนลดต้องเป็นจำนวนเต็ม 0–100")]
    [InlineData("qtyA", 0, "จำนวนต่อชุดต้องเป็นจำนวนเต็มมากกว่า 0")]
    [InlineData("qtyA", 1.5, "จำนวนต่อชุดต้องเป็นจำนวนเต็มมากกว่า 0")]
    public async Task A_number_out_of_its_range_is_refused_under_its_field(string field, double value, string message)
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var body = new Dictionary<string, object?>
        {
            ["name"] = "ซื้อ 3 ลด 20%", ["type"] = "BUY_X_PERCENT", ["productA"] = orange, ["qtyA"] = 3, ["ratePercent"] = 20, ["startDate"] = "2026-09-01",
            [field] = value,
        };

        var response = await Create(owner, body);

        Assert.Equal([(field, message)], await Errors(response));
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<TaladDbContext>().Promotions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.005)]
    public async Task A_minimum_below_0_or_finer_than_satang_is_refused(double min)
    {
        var owner = await Owner();

        var response = await Create(owner, new { name = "ลดทั้งบิล", type = "BILL_PERCENT", ratePercent = 5, minSubtotal = (decimal)min, startDate = "2026-09-01" });

        Assert.Equal([("minSubtotal", "ยอดขั้นต่ำต้องไม่ติดลบ และมีทศนิยมไม่เกิน 2 ตำแหน่ง")], await Errors(response));
    }

    [Theory]
    [InlineData(null, null, "startDate", "กรุณาระบุวันเริ่ม")]                     // BR-talad-015@v1 — a start date always
    [InlineData("2026-13-01", null, "startDate", "วันที่ไม่ถูกต้อง")]
    [InlineData("2026-09-10", "10/09/2026", "endDate", "วันที่ไม่ถูกต้อง")]
    [InlineData("2026-09-10", "2026-09-09", "endDate", "วันสิ้นสุดต้องไม่ก่อนวันเริ่ม")]
    public async Task Dates_are_whole_days_with_a_start_and_an_end_never_before_it(string? start, string? end, string field, string message)
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");

        var response = await Create(owner, new { name = "ส้ม ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = start, endDate = end });

        Assert.Equal([(field, message)], await Errors(response));
    }

    [Fact]
    public async Task No_name_and_no_form_are_both_answered()
    {
        var owner = await Owner();

        var response = await Create(owner, new { name = " ", type = "BUY_ONE_GET_ALL", startDate = "2026-09-01" });

        Assert.Equal([("name", "กรุณากรอกช่องนี้"), ("type", "กรุณาเลือกรูปแบบโปรโมชั่น")], await Errors(response));
    }

    [Fact]
    public async Task A_discontinued_or_unknown_product_is_refused_under_its_field()
    {
        var owner = await Owner();
        var sold = SeedProduct("ส้มสายน้ำผึ้ง");
        var gone = SeedProduct("ส้มเก่า", discontinued: true);

        var response = await Create(owner, new { name = "ซื้อ a + b แถม", type = "BUY_AB_GET_Y", productA = sold, qtyA = 1, productB = gone, qtyB = 1, freeProduct = 999_999, freeQty = 1, startDate = "2026-09-01" });

        Assert.Equal([("productB", "ไม่พบสินค้านี้ หรือสินค้าเลิกขายแล้ว"), ("freeProduct", "ไม่พบสินค้านี้ หรือสินค้าเลิกขายแล้ว")], await Errors(response));
    }

    // ── API-025 · 026 ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_shows_active_promotions_by_part_of_their_name_20_a_page()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        for (var i = 0; i < 21; i++)
            await Create(owner, new { name = $"ส้ม {i:D2}", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" });
        var gone = await Created(await Create(owner, new { name = "ส้ม เลิกแล้ว", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" }));
        await Create(owner, new { name = "มังคุด", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" });
        Discontinue(gone.Id);

        var first = (await owner.GetFromJsonAsync<PagedResult<PromotionView>>("/api/promotions?search=ส้ม"))!;
        var second = (await owner.GetFromJsonAsync<PagedResult<PromotionView>>("/api/promotions?search=ส้ม&page=2"))!;

        Assert.Equal((20, 21, 20), (first.Items.Count, first.Total, first.PageSize));
        Assert.Equal("ส้ม 00", first.Items[0].Name);
        Assert.Equal(["ส้ม 20"], second.Items.Select(p => p.Name));
        Assert.Equal("ส้มสายน้ำผึ้ง", first.Items[0].ProductA!.Name);
    }

    [Fact]
    public async Task A_discontinued_or_unknown_promotion_is_not_found_and_not_edited()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var body = new { name = "ส้ม ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" };
        var promotion = await Created(await Create(owner, body));
        Discontinue(promotion.Id);

        var open = await owner.GetAsync($"/api/promotions/{promotion.Id}");
        var edit = await owner.PostAsJsonAsync($"/api/promotions/{promotion.Id}/versions", body);
        var unknown = await owner.GetAsync("/api/promotions/999999");

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound), (open.StatusCode, edit.StatusCode, unknown.StatusCode));
        Assert.Single(StoredVersions(promotion.Id));
    }

    // ── ACL-023 — the owner's alone ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seller_can_neither_list_nor_create()
    {
        await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง");
        var somchai = await SignedInAs("somchai", UserRole.Cashier);

        var list = await somchai.GetAsync("/api/promotions");
        var create = await Create(somchai, new { name = "ส้ม ลด 10%", type = "ITEM_PERCENT", productA = orange, ratePercent = 10, startDate = "2026-09-01" });

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (list.StatusCode, create.StatusCode));
    }

    [Fact]
    public void The_domain_refuses_a_seller_even_without_the_endpoint_policy()
    {
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);

        Assert.Throws<PromotionOwnerOnlyException>(() => Promotion.Open(seller, DateTimeOffset.UnixEpoch));
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_every_promotion_call_answers_401()
    {
        var anonymous = _factory.CreateClient();

        var list = await anonymous.GetAsync("/api/promotions");
        var create = await Create(anonymous, new { name = "x" });

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (list.StatusCode, create.StatusCode));
    }

    private static Dictionary<string, object?> Merge(object a, object b)
    {
        var d = new Dictionary<string, object?>();
        foreach (var o in new[] { a, b })
            foreach (var p in o.GetType().GetProperties()) d[p.Name] = p.GetValue(o);
        return d;
    }
}
