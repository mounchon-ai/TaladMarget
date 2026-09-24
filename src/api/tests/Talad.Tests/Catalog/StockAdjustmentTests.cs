using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Catalog;
using Talad.Application.Catalog;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Catalog;

/// <summary>
/// UC-talad-020 · API-024 · API-019 — the owner records a hand change of stock (BR-talad-032@v1): RECEIVE and SPOILED
/// carry the change, RECOUNT carries what was counted and the change is worked out. Every save is a new ENT-003 row
/// with who, when and why, and one form saves once (BR-talad-041@v1). The PostgreSQL halves — the insert-only
/// trigger and the unique request_key index catching a real race — are proven on the real database, not here:
/// InMemory enforces neither.
/// </summary>
[Trait("feature", "FE-talad-023")]
public sealed class StockAdjustmentTests : IDisposable
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

    private int SeedProduct(string name, int stock, int threshold = 0, bool discontinued = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.First(a => a.Role == UserRole.Owner);
        var product = new Product(name, null, stock, threshold, DateTimeOffset.UnixEpoch);
        db.Products.Add(product);
        db.SaveChanges();
        var version = new ProductPriceVersion(product.Id, 45m, null, PriceChangeSource.StockScreen, owner.Id, DateTimeOffset.UnixEpoch);
        db.ProductPriceVersions.Add(version);
        db.SaveChanges();
        product.PointAtPrice(version);
        if (discontinued) product.Discontinue(owner);
        db.SaveChanges();
        return product.Id;
    }

    private static object Form(string reason, int? quantity = null, int? counted = null, string? note = null, string? key = null) =>
        new { reason, quantity, countedQty = counted, note, requestKey = key ?? Guid.NewGuid().ToString() };

    private static Task<HttpResponseMessage> Adjust(HttpClient client, int id, object form) =>
        client.PostAsJsonAsync($"/api/products/{id}/stock-adjustments", form);

    private static async Task<ProductDetail> Saved(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDetail>())!;
    }

    private static async Task<(HttpStatusCode Status, string Code, string Field, string Message)> Refusal(HttpResponseMessage response)
    {
        var body = (await response.Content.ReadFromJsonAsync<ProductError>())!;
        var error = Assert.Single(body.Errors);
        return (response.StatusCode, body.Code, error.Field, error.Message);
    }

    private (int StockQty, List<StockAdjustment> Rows) Stored(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var stock = db.Products.AsNoTracking().Single(p => p.Id == productId).StockQty;
        return (stock, db.StockAdjustments.AsNoTracking().Where(a => a.ProductId == productId).OrderBy(a => a.Id).ToList());
    }

    private static readonly UserAccount OwnerAccount = new("owner", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);

    // ── AC-talad-075 — RECEIVE +10: 4 → 14, with who, when and why ───────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-075")]
    public async Task Receiving_10_oranges_makes_14_and_records_the_owner_the_time_and_the_reason()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);
        var sent = DateTimeOffset.UtcNow;

        var detail = await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: 10)));

        Assert.Equal(14, detail.StockQty);
        var row = Assert.Single(detail.Adjustments.Items);
        Assert.Equal(("RECEIVE", 10, (int?)null, "เจ้าของร้าน"), (row.Reason, row.QuantityDelta, row.CountedQty, row.AdjustedByName));
        Assert.InRange(row.AdjustedAt, sent.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(14, Stored(orange).StockQty);
    }

    [Fact, Trait("ac", "AC-talad-075")]
    public void The_domain_stamps_the_row_with_the_owner_and_the_time_it_was_given()
    {
        var orange = new Product("ส้มสายน้ำผึ้ง", null, 4, 0, DateTimeOffset.UnixEpoch);
        var nineTen = new DateTimeOffset(2026, 9, 23, 9, 10, 0, TimeSpan.FromHours(7)); // 23 ก.ย. 2569 09:10

        var row = orange.Adjust(StockAdjustmentReason.Receive, 10, null, "  ของจากสวน  ", "form-1", OwnerAccount, nineTen);

        Assert.Equal(14, orange.StockQty);
        Assert.Equal((StockAdjustmentReason.Receive, 10, "ของจากสวน", "form-1", nineTen), (row.Reason, row.QuantityDelta, row.Note, row.RequestKey, row.AdjustedAt));
    }

    // ── AC-talad-074 — +10 past the threshold takes the low-stock flag off both screens ──────────────────

    [Fact, Trait("ac", "AC-talad-074")]
    public async Task Receiving_past_the_threshold_takes_the_low_stock_flag_off_the_sales_screen_and_the_stock_list()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4, threshold: 5);
        var somchai = await SignedInAs("somchai", UserRole.Cashier);
        async Task<bool> LowStock(HttpClient client) =>
            (await client.GetFromJsonAsync<PagedResult<ProductCard>>("/api/products?search=ส้มสายน้ำผึ้ง"))!.Items.Single().LowStock;
        Assert.True(await LowStock(somchai));

        await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: 10)));

        Assert.Equal((false, false), (await LowStock(somchai), await LowStock(owner)));
    }

    // ── AC-talad-076 — RECOUNT 8 against 10: the system works out −2 ─────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-076")]
    public async Task Recounting_8_against_10_makes_8_and_records_minus_2()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 10);

        var detail = await Saved(await Adjust(owner, orange, Form("RECOUNT", counted: 8)));

        Assert.Equal(8, detail.StockQty);
        var row = Assert.Single(detail.Adjustments.Items);
        Assert.Equal(("RECOUNT", -2, (int?)8), (row.Reason, row.QuantityDelta, row.CountedQty));
    }

    // ── AC-talad-077 — below zero is refused with the rule's sentence ───────────────────────────────────

    [Fact, Trait("ac", "AC-talad-077")]
    public async Task Spoiling_5_of_3_is_refused_and_nothing_changes()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 3);

        var refused = await Refusal(await Adjust(owner, orange, Form("SPOILED", quantity: -5)));

        Assert.Equal((HttpStatusCode.BadRequest, "STOCK_ADJUSTMENT_INVALID", "quantity", "จำนวนคงเหลือต้องไม่ติดลบ (เหลือ 3)"), refused);
        var stored = Stored(orange);
        Assert.Equal(3, stored.StockQty);
        Assert.Empty(stored.Rows);
    }

    // ── AC-talad-078 — a second adjustment is a second row; the first is not overwritten ──────────────────

    [Fact, Trait("ac", "AC-talad-078")]
    public async Task Plus_10_then_minus_2_makes_12_with_both_rows_newest_first()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);

        await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: 10)));
        var detail = await Saved(await Adjust(owner, orange, Form("SPOILED", quantity: -2)));

        Assert.Equal(12, detail.StockQty);
        Assert.Equal([("SPOILED", -2), ("RECEIVE", 10)], detail.Adjustments.Items.Select(a => (a.Reason, a.QuantityDelta)));
        Assert.Equal(2, detail.Adjustments.Total);
    }

    // ── AC-talad-079 · 080 — one form saves once, whether clicked twice or resent after the line dropped ─────

    [Theory, Trait("ac", "AC-talad-079"), Trait("ac", "AC-talad-080")]
    [InlineData("AC-talad-079")]
    [InlineData("AC-talad-080")]
    public async Task The_same_form_sent_twice_makes_one_row_and_14_not_24(string _)
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);
        var form = Form("RECEIVE", quantity: 10, key: "form-079");

        await Saved(await Adjust(owner, orange, form));
        var again = await Refusal(await Adjust(owner, orange, form));

        Assert.Equal((HttpStatusCode.Conflict, "STOCK_ADJUSTMENT_DUPLICATE", "requestKey", "รายการปรับสต็อกนี้บันทึกไปแล้ว"), again);
        var stored = Stored(orange);
        Assert.Equal(14, stored.StockQty);
        Assert.Equal([10], stored.Rows.Select(r => r.QuantityDelta));
    }

    [Fact, Trait("ac", "AC-talad-080")]
    public async Task A_resent_spoilage_that_emptied_the_shelf_answers_already_saved_not_below_zero()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 5);
        var form = Form("SPOILED", quantity: -5, key: "form-spoiled");

        await Saved(await Adjust(owner, orange, form));
        var again = await Refusal(await Adjust(owner, orange, form));

        Assert.Equal((HttpStatusCode.Conflict, "รายการปรับสต็อกนี้บันทึกไปแล้ว"), (again.Status, again.Message));
        Assert.Equal(0, Stored(orange).StockQty);
    }

    [Fact, Trait("ac", "AC-talad-080")]
    public async Task A_resent_recount_answers_already_saved_not_no_difference()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 10);
        var form = Form("RECOUNT", counted: 8, key: "form-recount");

        await Saved(await Adjust(owner, orange, form));
        var again = await Refusal(await Adjust(owner, orange, form));

        Assert.Equal((HttpStatusCode.Conflict, "รายการปรับสต็อกนี้บันทึกไปแล้ว"), (again.Status, again.Message));
        Assert.Single(Stored(orange).Rows);
    }

    // ── AC-talad-081 — a new form is a new key: two deliveries of 10 make 24 ─────────────────────────────

    [Fact, Trait("ac", "AC-talad-081")]
    public async Task Two_forms_of_plus_10_both_save_and_make_24()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);

        await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: 10)));
        var detail = await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: 10)));

        Assert.Equal(24, detail.StockQty);
        Assert.Equal([10, 10], detail.Adjustments.Items.Select(a => a.QuantityDelta));
    }

    // ── what design leaves unworded — every refusal under its own field, nothing changed ─────────────────

    public static TheoryData<object, string, string> Refused => new()
    {
        { new { reason = (string?)null, quantity = 10, requestKey = "k" }, "reason", "กรุณาเลือกเหตุผล" },
        { new { reason = "STOLEN", quantity = 10, requestKey = "k" }, "reason", "กรุณาเลือกเหตุผล" },
        { new { reason = "RECEIVE", quantity = 0, requestKey = "k" }, "quantity", "กรุณากรอกจำนวนเต็มที่ไม่ใช่ 0" },
        { new { reason = "RECEIVE", quantity = (int?)null, requestKey = "k" }, "quantity", "กรุณากรอกจำนวนเต็มที่ไม่ใช่ 0" },
        { new { reason = "RECEIVE", quantity = 1.5m, requestKey = "k" }, "quantity", "กรุณากรอกจำนวนเต็มที่ไม่ใช่ 0" },
        { new { reason = "RECOUNT", countedQty = (int?)null, requestKey = "k" }, "countedQty", "กรุณากรอกจำนวนที่นับได้เป็นจำนวนเต็ม 0 ขึ้นไป" },
        { new { reason = "RECOUNT", countedQty = -1, requestKey = "k" }, "countedQty", "กรุณากรอกจำนวนที่นับได้เป็นจำนวนเต็ม 0 ขึ้นไป" },
        { new { reason = "RECOUNT", countedQty = 4, requestKey = "k" }, "countedQty", "จำนวนที่นับได้เท่ากับคงเหลือในระบบ ไม่มีส่วนต่างให้บันทึก" },
        { new { reason = "RECEIVE", quantity = int.MaxValue, requestKey = "k" }, "quantity", "จำนวนมากเกินกว่าที่ระบบรับได้" },
        { new { reason = "RECEIVE", quantity = 10, requestKey = " " }, "requestKey", "ฟอร์มนี้ไม่มีคีย์กันบันทึกซ้ำ กรุณาเปิดฟอร์มใหม่" },
    };

    [Theory, MemberData(nameof(Refused))]
    public async Task A_form_design_would_not_accept_is_refused_under_its_field_and_nothing_changes(object form, string field, string message)
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);

        var refused = await Refusal(await Adjust(owner, orange, form));

        Assert.Equal((HttpStatusCode.BadRequest, "STOCK_ADJUSTMENT_INVALID", field, message), refused);
        var stored = Stored(orange);
        Assert.Equal(4, stored.StockQty);
        Assert.Empty(stored.Rows);
    }

    // ── API-019 — the adjustments section pages on its own, 20 a page ───────────────────────────────────

    [Fact, Trait("nfr", "NFR-talad-006")]
    public async Task The_adjustments_section_pages_20_at_a_time_apart_from_the_price_history()
    {
        var owner = await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 0);
        for (var i = 1; i <= 21; i++) await Saved(await Adjust(owner, orange, Form("RECEIVE", quantity: i)));

        var second = (await owner.GetFromJsonAsync<ProductDetail>($"/api/products/{orange}?adjustmentsPage=2"))!;

        Assert.Equal((2, 20, 21), (second.Adjustments.Page, second.Adjustments.PageSize, second.Adjustments.Total));
        Assert.Equal([1], second.Adjustments.Items.Select(a => a.QuantityDelta)); // the oldest, last
        Assert.Equal(1, second.PriceHistory.Page);
    }

    // ── ACL-022 — the owner's alone, on a product still sold ────────────────────────────────────────────

    [Fact]
    public async Task A_seller_cannot_adjust_and_the_stock_stays()
    {
        await Owner();
        var orange = SeedProduct("ส้มสายน้ำผึ้ง", stock: 4);
        var somchai = await SignedInAs("somchai", UserRole.Cashier);

        var response = await Adjust(somchai, orange, Form("RECEIVE", quantity: 10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal((4, 0), (Stored(orange).StockQty, Stored(orange).Rows.Count));
    }

    [Fact]
    public void The_domain_refuses_a_seller_and_a_discontinued_product_even_without_the_endpoint()
    {
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        var orange = new Product("ส้มสายน้ำผึ้ง", null, 4, 0, DateTimeOffset.UnixEpoch);

        Assert.Throws<StockOwnerOnlyException>(() => orange.Adjust(StockAdjustmentReason.Receive, 10, null, null, "k", seller, DateTimeOffset.UnixEpoch));
        orange.Discontinue(OwnerAccount);
        Assert.Throws<ProductNotActiveException>(() => orange.Adjust(StockAdjustmentReason.Receive, 10, null, null, "k", OwnerAccount, DateTimeOffset.UnixEpoch));
        Assert.Equal(4, orange.StockQty);
    }

    [Fact]
    public async Task A_discontinued_or_unknown_product_is_not_found()
    {
        var owner = await Owner();
        var gone = SeedProduct("ขนมปังเลิกขาย", stock: 4, discontinued: true);

        var discontinued = await Adjust(owner, gone, Form("RECEIVE", quantity: 10));
        var unknown = await Adjust(owner, 999999, Form("RECEIVE", quantity: 10));

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (discontinued.StatusCode, unknown.StatusCode));
        Assert.Equal(4, Stored(gone).StockQty);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_adjusting_answers_401()
    {
        var response = await Adjust(_factory.CreateClient(), 1, Form("RECEIVE", quantity: 10));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
