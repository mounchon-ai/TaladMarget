using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Sales;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Sales;

/// <summary>
/// UC-talad-004 · API-009 — the caller's own cart priced at the moment of the call: prices, promotions and the member rate
/// in force then (BR-talad-038@v1), promotion days by the Thai calendar (BR-talad-015@v1). The arithmetic is proven
/// row by row against req's answer keys in <see cref="PricingGoldenTests"/>; this proves the wiring — every promotion
/// type the owner saves reaches the engine as the type it is, with the numbers the acceptance criteria give.
/// "At checkout" in AC-talad-023..026 is "when API-009 is called" here: paying is FE-talad-033's. The bill halves —
/// AC-talad-065's record, AC-talad-095's accumulated amount — need a bill, which does not exist yet.
/// </summary>
[Trait("feature", "FE-talad-031")]
public sealed class CartPricingApiTests : IDisposable
{
    /// <summary>
    /// Starts at the real now: tokens are stamped with this clock but checked by JwtBearer against the real one, so a
    /// test moves it only after signing in.
    /// </summary>
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }

    private static DateTimeOffset Thai(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));

    private readonly TaladApiFactory _base = new();
    private readonly Clock _clock = new();
    private readonly WebApplicationFactory<Program> _app;

    public CartPricingApiTests() =>
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

    private async Task<HttpClient> SignedInAs(string username, UserRole role)
    {
        Db(db =>
        {
            using var scope = _app.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IdentityPasswordHasher>();
            var account = new UserAccount(username, role == UserRole.Owner ? "เจ้าของร้าน" : username, role, DateTimeOffset.UnixEpoch);
            account.SetPasswordHash(hasher.Hash(account, "Pass#2569"));
            return db.UserAccounts.Add(account);
        });
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> Owner() => SignedInAs("owner", UserRole.Owner);

    private Task<HttpClient> Somchai() => SignedInAs("somchai", UserRole.Cashier);

    /// <summary>The products the acceptance criteria name, at the prices they give.</summary>
    private Dictionary<string, int> Shop(params (string Name, decimal Price, int Stock)[] extra)
    {
        var items = new (string Name, decimal Price, int Stock)[] { ("ส้มสายน้ำผึ้ง", 45m, 50), ("มังคุด แพ็ก", 120m, 50), ("ลำไย", 19.75m, 50), ("ฝรั่ง", 24.25m, 50) }
            .Where(i => extra.All(e => e.Name != i.Name)).Concat(extra);
        return items.ToDictionary(i => i.Name, i => Product(i.Name, i.Price, i.Stock));
    }

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

    private int Member(string name, string phone) => Db(db =>
    {
        var m = Talad.Domain.Members.Member.Register(name, phone, 1, DateTimeOffset.UnixEpoch);
        db.Members.Add(m);
        return m;
    }).Id;

    private static async Task<int> Promotion(HttpClient owner, object body)
    {
        var response = await owner.PostAsJsonAsync("/api/promotions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Talad.Application.Promotions.PromotionView>())!.Id;
    }

    private static object Percent(string name, int a, int rate, string start = "2026-09-01", string? end = null) =>
        new { name, type = "ITEM_PERCENT", productA = a, ratePercent = rate, startDate = start, endDate = end };
    private static object BuyXPercent(string name, int a, int x, int y) =>
        new { name, type = "BUY_X_PERCENT", productA = a, qtyA = x, ratePercent = y, startDate = "2026-09-01" };
    private static object BuyAbPercent(string name, int a, int na, int b, int nb, int y) =>
        new { name, type = "BUY_AB_PERCENT", productA = a, qtyA = na, productB = b, qtyB = nb, ratePercent = y, startDate = "2026-09-01" };
    private static object BuyXGetY(string name, int a, int x, int free, int y) =>
        new { name, type = "BUY_X_GET_Y", productA = a, qtyA = x, freeProduct = free, freeQty = y, startDate = "2026-09-01" };
    private static object BuyAbGetY(string name, int a, int na, int b, int nb, int free, int y) =>
        new { name, type = "BUY_AB_GET_Y", productA = a, qtyA = na, productB = b, qtyB = nb, freeProduct = free, freeQty = y, startDate = "2026-09-01" };
    private static object Bill(string name, int rate, decimal min) =>
        new { name, type = "BILL_PERCENT", ratePercent = rate, minSubtotal = min, startDate = "2026-09-01" };

    private static async Task MemberRate(HttpClient owner, int rate) =>
        Assert.True((await owner.PostAsJsonAsync("/api/settings/member-discount", new { ratePercent = rate })).IsSuccessStatusCode);

    private static async Task Put(HttpClient cashier, int product, int qty)
    {
        Assert.Equal(HttpStatusCode.OK, (await cashier.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(product))).StatusCode);
        if (qty > 1) Assert.Equal(HttpStatusCode.OK, (await cashier.PatchAsJsonAsync($"/api/cart/lines/{product}", new SetQtyRequest(qty))).StatusCode);
    }

    private static async Task Bind(HttpClient cashier, int member) =>
        Assert.Equal(HttpStatusCode.OK, (await cashier.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(member))).StatusCode);

    private static async Task<CartPricingView> Priced(HttpClient cashier, params int[] choice)
    {
        var query = string.Join("&", choice.Select(c => $"choice={c}"));
        var response = await cashier.GetAsync($"/api/cart/pricing{(query.Length > 0 ? "?" + query : "")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartPricingView>())!;
    }

    // ── BR-talad-038@v1 — the price and the member rate in force when priced ────────────────────────────────

    [Fact, Trait("ac", "AC-talad-023")]
    public async Task A_price_changed_while_the_cart_is_open_is_the_one_counted()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 2);

        await owner.PostAsJsonAsync($"/api/products/{shop["ส้มสายน้ำผึ้ง"]}/prices", new { price = 50, source = "STOCK_SCREEN" });
        var priced = await Priced(somchai);

        Assert.Equal((50m, 100m), (priced.Lines.Single().UnitPrice, priced.Net!.Value));
    }

    [Fact, Trait("ac", "AC-talad-024")]
    public async Task A_member_rate_changed_while_the_cart_is_open_is_the_one_counted()
    {
        var owner = await Owner();
        Shop(("ของ 100 บาท", 100m, 10));
        var somchai = await Somchai();
        await MemberRate(owner, 5);
        await Put(somchai, Db(db => db.Products.Single(p => p.Name == "ของ 100 บาท").Id), 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));

        await MemberRate(owner, 10);
        var priced = await Priced(somchai);

        Assert.Equal((10, 10m, 90m), (priced.MemberRate!.Value, priced.MemberDiscount!.Value, priced.Net!.Value));
        Assert.Equal("สมหญิง ใจดี", priced.Member!.Name);
    }

    // ── BR-talad-015@v1 — a promotion's days are Thai calendar days, both ends counted in full ──────────────

    public static TheoryData<string, string, string?, DateTimeOffset, decimal> PromotionDays => new()
    {
        // AC-talad-025 — ended on the 23rd; priced at 00:01 on the 24th (still the 23rd in UTC)
        { "AC-talad-025", "2026-09-01", "2026-09-23", Thai(2026, 9, 24, 0, 1), 90m },
        { "AC-talad-025", "2026-09-01", "2026-09-23", Thai(2026, 9, 23, 23, 55), 81m },
        // AC-talad-026 — starts on the 24th; 00:01 on the 24th has it
        { "AC-talad-026", "2026-09-24", null, Thai(2026, 9, 24, 0, 1), 81m },
        // AC-talad-100 — the last day, 23:58
        { "AC-talad-100", "2026-09-23", "2026-09-25", Thai(2026, 9, 25, 23, 58), 81m },
        // AC-talad-101 — no end date, 31 Dec
        { "AC-talad-101", "2026-09-01", null, Thai(2026, 12, 31, 12, 0), 81m },
        // AC-talad-102 — the day before the start, 23:59
        { "AC-talad-102", "2026-09-24", null, Thai(2026, 9, 23, 23, 59), 90m },
    };

    [Theory, MemberData(nameof(PromotionDays))]
    public async Task A_promotion_counts_on_its_days_by_the_Thai_calendar(string ac, string start, string? end, DateTimeOffset now, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", shop["ส้มสายน้ำผึ้ง"], 10, start, end));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 2);

        _clock.Now = now;
        var priced = await Priced(somchai);

        Assert.True(net == priced.Net, $"{ac}: net {priced.Net} at {now:yyyy-MM-dd HH:mm zzz}, expected {net}");
    }

    // ── BR-talad-028@v1 · BR-talad-027@v1 · BR-talad-010@v1 — one after the other, rounded half up ─────────────

    [Theory, Trait("ac", "AC-talad-065"), Trait("ac", "AC-talad-091")]
    [InlineData(false, 9, 0, 10.05, 190.95)]
    [InlineData(true, 9, 20.10, 9.05, 171.85)]
    public async Task Item_promotion_then_whole_bill_then_member(bool billPromotion, decimal promo, decimal bill, decimal member, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", shop["ส้มสายน้ำผึ้ง"], 10));
        if (billPromotion) await Promotion(owner, Bill("ลดทั้งบิล 10%", 10, 0m));
        await MemberRate(owner, 5);
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 2);
        await Put(somchai, shop["มังคุด แพ็ก"], 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));

        var priced = await Priced(somchai);

        Assert.Equal((promo, bill, member, net), (priced.PromoDiscount!.Value, priced.BillDiscount!.Value, priced.MemberDiscount!.Value, priced.Net!.Value));
        Assert.Equal(billPromotion ? "ลดทั้งบิล 10%" : null, priced.BillPromotion?.Name);
    }

    [Theory]
    [InlineData("AC-talad-092", "ส้มสายน้ำผึ้ง", 100, 45.00, 0.00)]
    [InlineData("AC-talad-093", "ลำไย", 15, 2.96, 16.79)]
    [InlineData("AC-talad-095", "มังคุด แพ็ก", 0, 0.00, 120.00)]
    public async Task The_member_rate_on_one_line(string ac, string product, int rate, decimal member, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await MemberRate(owner, rate);
        await Put(somchai, shop[product], 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));

        var priced = await Priced(somchai);

        Assert.True((member, net) == (priced.MemberDiscount, priced.Net), $"{ac}: {priced.MemberDiscount} · {priced.Net}");
    }

    [Fact, Trait("ac", "AC-talad-094")]
    public async Task Every_member_gets_the_same_rate()
    {
        var owner = await Owner();
        Shop(("ของ 100 บาท", 100m, 10));
        var item = Db(db => db.Products.Single(p => p.Name == "ของ 100 บาท").Id);
        var somchai = await Somchai();
        var manee = await SignedInAs("manee", UserRole.Cashier);
        await MemberRate(owner, 5);
        await Put(somchai, item, 1);
        await Put(manee, item, 1);
        await Bind(somchai, Member("สมหญิง ใจดี", "0811111111"));
        await Bind(manee, Member("สมหมาย รักดี", "0822222222"));

        var (a, b) = (await Priced(somchai), await Priced(manee));

        Assert.Equal((5m, 95m, 5m, 95m), (a.MemberDiscount!.Value, a.Net!.Value, b.MemberDiscount!.Value, b.Net!.Value));
    }

    [Fact]
    public async Task A_member_hidden_after_being_bound_gets_no_member_discount()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await MemberRate(owner, 5);
        await Put(somchai, shop["มังคุด แพ็ก"], 1);
        var member = Member("สมหญิง ใจดี", "0811111111");
        await Bind(somchai, member);
        Db(db =>
        {
            db.Members.Single(m => m.Id == member).Hide(db.UserAccounts.First(a => a.Role == UserRole.Owner), DateTimeOffset.UnixEpoch);
            return 0;
        });

        var priced = await Priced(somchai);

        Assert.Equal((0, 0m, 120m), (priced.MemberRate!.Value, priced.MemberDiscount!.Value, priced.Net!.Value));
        Assert.Null(priced.Member);
    }

    // ── BR-talad-009@v1 — % per item on the whole line, and the whole-bill minimum ─────────────────────────────

    [Fact, Trait("ac", "AC-talad-096")]
    public async Task Seven_percent_off_three_longans_is_worked_out_on_the_whole_line()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, Percent("ลำไย ลด 7%", shop["ลำไย"], 7));
        await Put(somchai, shop["ลำไย"], 3);

        var priced = await Priced(somchai);

        Assert.Equal((4.15m, 55.10m), (priced.PromoDiscount!.Value, priced.Net!.Value));
        Assert.Equal("ลำไย ลด 7%", priced.Lines.Single().Promotion!.Name);
    }

    [Theory]
    [InlineData("AC-talad-097", 500.00, 25.00, 475.00)]
    [InlineData("AC-talad-098", 499.99, 0.00, 499.99)]
    [InlineData("AC-talad-099", 1000.00, 100.00, 900.00)]
    public async Task The_whole_bill_discount_needs_its_minimum_and_only_the_best_one_counts(string ac, decimal total, decimal bill, decimal net)
    {
        var owner = await Owner();
        var item = Product("ของรวม", total, 10);
        var somchai = await Somchai();
        await Promotion(owner, Bill("ลดทั้งบิล 5%", 5, 500m));
        await Promotion(owner, Bill("ลดทั้งบิล 10%", 10, 1000m));
        await Put(somchai, item, 1);

        var priced = await Priced(somchai);

        Assert.True((bill, net) == (priced.BillDiscount, priced.Net), $"{ac}: {priced.BillDiscount} · {priced.Net}");
    }

    // ── BR-talad-011@v1 · BR-talad-012@v1 — free gifts the staff puts in the cart ─────────────────────────────

    [Theory]
    [InlineData("AC-talad-109", 5, 2, 240.00, 225.00)]
    [InlineData("AC-talad-111", 2, 3, 120.00, 330.00)]
    public async Task Buy_two_oranges_get_a_mangosteen(string ac, int oranges, int mangosteens, decimal promo, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyXGetY("ซื้อ ส้ม 2 แถม มังคุด 1", shop["ส้มสายน้ำผึ้ง"], 2, shop["มังคุด แพ็ก"], 1));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], oranges);
        await Put(somchai, shop["มังคุด แพ็ก"], mangosteens);

        var priced = await Priced(somchai);

        Assert.True((promo, net) == (priced.PromoDiscount, priced.Net), $"{ac}: {priced.PromoDiscount} · {priced.Net}");
        Assert.All(priced.Lines, l => Assert.Equal("ซื้อ ส้ม 2 แถม มังคุด 1", l.Promotion!.Name));
    }

    [Theory]
    [InlineData("AC-talad-110", "มังคุด แพ็ก")]
    [InlineData("AC-talad-114", "ลำไย")]
    public async Task A_gift_out_of_stock_does_not_go_in_and_the_rest_sells_at_its_price(string ac, string gift)
    {
        var owner = await Owner();
        var shop = Shop((gift, gift == "ลำไย" ? 19.75m : 120m, 0));
        var somchai = await Somchai();
        if (ac == "AC-talad-110") await Promotion(owner, BuyXGetY("ซื้อ ส้ม 2 แถม มังคุด 1", shop["ส้มสายน้ำผึ้ง"], 2, shop["มังคุด แพ็ก"], 1));
        else await Promotion(owner, BuyAbGetY("ส้ม 1 + มังคุด 1 แถม ลำไย 1", shop["ส้มสายน้ำผึ้ง"], 1, shop["มังคุด แพ็ก"], 1, shop["ลำไย"], 1));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], ac == "AC-talad-110" ? 2 : 1);
        if (ac == "AC-talad-114") await Put(somchai, shop["มังคุด แพ็ก"], 1);

        var refused = await somchai.PostAsJsonAsync("/api/cart/lines", new AddLineRequest(shop[gift]));
        var priced = await Priced(somchai);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal($"{gift} คงเหลือไม่พอ (เหลือ 0)", (await refused.Content.ReadFromJsonAsync<CartError>())!.Message);
        Assert.Equal(ac == "AC-talad-110" ? 90m : 165m, priced.Net);
        Assert.DoesNotContain(priced.Lines, l => l.ProductId == shop[gift]);
    }

    [Fact, Trait("ac", "AC-talad-112")]
    public async Task Buy_two_oranges_get_one_orange()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyXGetY("ซื้อ ส้ม 2 แถม ส้ม 1", shop["ส้มสายน้ำผึ้ง"], 2, shop["ส้มสายน้ำผึ้ง"], 1));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 5);

        var priced = await Priced(somchai);

        Assert.Equal((45m, 180m), (priced.PromoDiscount!.Value, priced.Net!.Value));
    }

    [Theory]
    [InlineData("AC-talad-113", "ลำไย", 4, 2, 2, 39.50, 420.00)]
    [InlineData("AC-talad-115", "ส้มสายน้ำผึ้ง", 3, 1, 0, 45.00, 210.00)]
    [InlineData("AC-talad-116", "ส้มสายน้ำผึ้ง", 2, 1, 0, 0.00, 210.00)]
    public async Task Buy_two_oranges_and_a_mangosteen_get_one(string ac, string gift, int oranges, int mangosteens, int longans, decimal promo, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyAbGetY($"ส้ม 2 + มังคุด 1 แถม {gift} 1", shop["ส้มสายน้ำผึ้ง"], 2, shop["มังคุด แพ็ก"], 1, shop[gift], 1));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], oranges);
        await Put(somchai, shop["มังคุด แพ็ก"], mangosteens);
        if (longans > 0) await Put(somchai, shop["ลำไย"], longans);

        var priced = await Priced(somchai);

        Assert.True((promo, net) == (priced.PromoDiscount, priced.Net), $"{ac}: {priced.PromoDiscount} · {priced.Net}");
    }

    // ── BR-talad-013@v1 · BR-talad-014@v1 — % off full sets only ──────────────────────────────────────────

    [Theory]
    [InlineData("AC-talad-117", "ส้มสายน้ำผึ้ง", 2, "มังคุด แพ็ก", 1, 10, 4, 2, 42.00, 378.00)]
    [InlineData("AC-talad-118", "ส้มสายน้ำผึ้ง", 2, "มังคุด แพ็ก", 1, 10, 1, 1, 0.00, 165.00)]
    [InlineData("AC-talad-119", "ส้มสายน้ำผึ้ง", 1, "มังคุด แพ็ก", 1, 10, 3, 1, 16.50, 238.50)]
    [InlineData("AC-talad-120", "ลำไย", 1, "ฝรั่ง", 1, 2, 1, 1, 0.89, 43.11)]
    public async Task Buy_a_and_b_percent_off(string ac, string a, int na, string b, int nb, int y, int qa, int qb, decimal promo, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyAbPercent($"{a} {na} + {b} {nb} ลด {y}%", shop[a], na, shop[b], nb, y));
        await Put(somchai, shop[a], qa);
        await Put(somchai, shop[b], qb);

        var priced = await Priced(somchai);

        Assert.True((promo, net) == (priced.PromoDiscount, priced.Net), $"{ac}: {priced.PromoDiscount} · {priced.Net}");
    }

    [Theory]
    [InlineData("AC-talad-121", "ส้มสายน้ำผึ้ง", 3, 10, 7, 27.00, 288.00)]
    [InlineData("AC-talad-122", "ส้มสายน้ำผึ้ง", 3, 10, 2, 0.00, 90.00)]
    [InlineData("AC-talad-123", "ส้มสายน้ำผึ้ง", 3, 10, 3, 13.50, 121.50)]
    [InlineData("AC-talad-124", "ลำไย", 3, 1, 6, 1.19, 117.31)]
    public async Task Buy_x_percent_off(string ac, string product, int x, int y, int qty, decimal promo, decimal net)
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyXPercent($"ซื้อ {product} {x} ชิ้น ลด {y}%", shop[product], x, y));
        await Put(somchai, shop[product], qty);

        var priced = await Priced(somchai);

        Assert.True((promo, net) == (priced.PromoDiscount, priced.Net), $"{ac}: {priced.PromoDiscount} · {priced.Net}");
    }

    // ── BR-talad-029@v1 · BR-talad-016@v2 — the promotion that gives most, named under each line ───────────────

    [Fact, Trait("ac", "AC-talad-125")]
    public async Task The_bigger_promotion_takes_the_oranges_and_the_mangosteen_has_none()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyAbPercent("ส้มสายน้ำผึ้ง 1 + มังคุด แพ็ก 1 ลด 10%", shop["ส้มสายน้ำผึ้ง"], 1, shop["มังคุด แพ็ก"], 1, 10));
        await Promotion(owner, BuyXPercent("ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 20%", shop["ส้มสายน้ำผึ้ง"], 3, 20));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 4);
        await Put(somchai, shop["มังคุด แพ็ก"], 1);

        var priced = await Priced(somchai);

        Assert.Equal(
            [("ส้มสายน้ำผึ้ง", "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 20%", 27m), ("มังคุด แพ็ก", (string?)null, 0m)],
            priced.Lines.Select(l => (l.Name, l.Promotion?.Name, l.ItemPromoDiscount!.Value)));
        Assert.Equal((27m, 273m), (priced.PromoDiscount!.Value, priced.Net!.Value));
    }

    [Fact, Trait("ac", "AC-talad-126")]
    public async Task A_promotion_that_gives_nothing_takes_no_line()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, BuyAbPercent("ส้มสายน้ำผึ้ง 2 + มังคุด แพ็ก 1 ลด 10%", shop["ส้มสายน้ำผึ้ง"], 2, shop["มังคุด แพ็ก"], 1, 10));
        await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 0%", shop["ส้มสายน้ำผึ้ง"], 0));
        await Promotion(owner, Percent("มังคุด แพ็ก ลด 5%", shop["มังคุด แพ็ก"], 5));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 1);
        await Put(somchai, shop["มังคุด แพ็ก"], 1);

        var priced = await Priced(somchai);

        Assert.Equal([(string?)null, "มังคุด แพ็ก ลด 5%"], priced.Lines.Select(l => l.Promotion?.Name));
        Assert.Equal((6m, 159m), (priced.PromoDiscount!.Value, priced.Net!.Value));
    }

    [Fact, Trait("ac", "AC-talad-127")]
    public async Task Two_promotions_that_give_the_same_wait_for_the_staff_to_choose()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        var tenPercent = await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", shop["ส้มสายน้ำผึ้ง"], 10));
        var threeForTen = await Promotion(owner, BuyXPercent("ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%", shop["ส้มสายน้ำผึ้ง"], 3, 10));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 3);

        var asked = await Priced(somchai);
        var wrong = await Priced(somchai, 999999);
        var chosen = await Priced(somchai, tenPercent);

        Assert.Equal([(tenPercent, "ส้มสายน้ำผึ้ง ลด 10%"), (threeForTen, "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%")], asked.NeedsChoice!.Select(p => (p.Id, p.Name)));
        Assert.Equal((135m, (decimal?)null, (decimal?)null), (asked.Lines.Single().LineGross, asked.Lines.Single().ItemPromoDiscount, asked.Net));
        Assert.NotNull(wrong.NeedsChoice);
        Assert.Null(chosen.NeedsChoice);
        Assert.Equal(("ส้มสายน้ำผึ้ง ลด 10%", 13.50m, 121.50m), (chosen.Lines.Single().Promotion!.Name, chosen.PromoDiscount!.Value, chosen.Net!.Value));
    }

    [Fact, Trait("ac", "AC-talad-128")]
    public async Task A_set_promotion_shows_its_part_under_each_of_its_lines()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Promotion(owner, Percent("ลำไย ลด 36%", shop["ลำไย"], 36));
        await Promotion(owner, BuyAbPercent("ลำไย 1 + ส้มสายน้ำผึ้ง 1 ลด 11%", shop["ลำไย"], 1, shop["ส้มสายน้ำผึ้ง"], 1, 11));
        await Put(somchai, shop["ลำไย"], 1);
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 1);

        var priced = await Priced(somchai);

        Assert.Equal(
            [("ลำไย", "ลำไย 1 + ส้มสายน้ำผึ้ง 1 ลด 11%", 2.17m), ("ส้มสายน้ำผึ้ง", "ลำไย 1 + ส้มสายน้ำผึ้ง 1 ลด 11%", 4.95m)],
            priced.Lines.Select(l => (l.Name, l.Promotion!.Name, l.ItemPromoDiscount!.Value)));
        Assert.Equal((7.12m, 57.63m), (priced.PromoDiscount!.Value, priced.Net!.Value));
    }

    // ── what is no longer there does not count, and does not break the page ──────────────────────────────────

    [Fact]
    public async Task A_discontinued_promotion_gives_nothing_and_a_discontinued_line_is_still_priced()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        var promotion = await Promotion(owner, Percent("ส้มสายน้ำผึ้ง ลด 10%", shop["ส้มสายน้ำผึ้ง"], 10));
        await Put(somchai, shop["ส้มสายน้ำผึ้ง"], 2);
        await Put(somchai, shop["มังคุด แพ็ก"], 1);

        await owner.PostAsync($"/api/promotions/{promotion}/discontinue", null);
        await owner.PostAsync($"/api/products/{shop["มังคุด แพ็ก"]}/discontinue", null);
        var priced = await Priced(somchai);

        Assert.Equal((0m, 210m), (priced.PromoDiscount!.Value, priced.Net!.Value));
        Assert.Equal(2, priced.Lines.Count);
    }

    [Fact]
    public async Task An_empty_cart_prices_to_nothing()
    {
        await Owner();
        var priced = await Priced(await Somchai());

        Assert.Empty(priced.Lines);
        Assert.Equal((0m, 0m), (priced.Subtotal!.Value, priced.Net!.Value));
    }

    // ── BR-talad-020@v1 · NFR-talad-005 ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Each_person_prices_their_own_cart_only()
    {
        var owner = await Owner();
        var shop = Shop();
        var somchai = await Somchai();
        await Put(somchai, shop["มังคุด แพ็ก"], 1);
        await Put(owner, shop["ส้มสายน้ำผึ้ง"], 1);

        var (mine, theirs) = (await Priced(somchai), await Priced(owner));

        Assert.Equal((120m, 45m), (mine.Net!.Value, theirs.Net!.Value));
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_pricing_answers_401()
    {
        var response = await _app.CreateClient().GetAsync("/api/cart/pricing");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
