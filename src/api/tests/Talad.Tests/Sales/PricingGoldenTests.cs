using System.Globalization;
using System.Text.Json;
using Talad.Domain.Promotions;
using Talad.Domain.Sales;

namespace Talad.Tests.Sales;

/// <summary>
/// CALC-talad-001..009 against req's signed answer keys GD-talad-001..009 (RQ24 · DV12). The rows are read from
/// <c>.aeon/req/requirements.json</c> as req wrote them — never copied, never worked out here — and each row is fed to
/// the entry point of the engine its contract describes. Every expected value is compared as the number req signed.
/// </summary>
[Trait("feature", "FE-talad-031")]
public sealed class PricingGoldenTests
{
    // ── the answer keys ─────────────────────────────────────────────────────────────────────────────

    private static readonly Lazy<Dictionary<string, JsonElement[]>> Goldens = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".aeon"))) dir = dir.Parent;
        var path = Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(".aeon not found above the test binaries"), ".aeon", "req", "requirements.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("goldenDatasets").EnumerateArray().ToDictionary(
            g => g.GetProperty("id").GetString()!,
            g => g.GetProperty("rows").EnumerateArray().Select(r => r.Clone()).ToArray());
    });

    private static TheoryData<int> RowsOf(string gd)
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Goldens.Value[gd].Length; i++) data.Add(i);
        return data;
    }

    public static TheoryData<int> Gd001 => RowsOf("GD-talad-001");
    public static TheoryData<int> Gd002 => RowsOf("GD-talad-002");
    public static TheoryData<int> Gd003 => RowsOf("GD-talad-003");
    public static TheoryData<int> Gd004 => RowsOf("GD-talad-004");
    public static TheoryData<int> Gd005 => RowsOf("GD-talad-005");
    public static TheoryData<int> Gd006 => RowsOf("GD-talad-006");
    public static TheoryData<int> Gd007 => RowsOf("GD-talad-007");
    public static TheoryData<int> Gd008 => RowsOf("GD-talad-008");
    public static TheoryData<int> Gd009 => RowsOf("GD-talad-009");

    private static (JsonElement In, JsonElement Out) Row(string gd, int i)
    {
        var row = Goldens.Value[gd][i];
        return (row.GetProperty("input"), row.GetProperty("expected"));
    }

    private static decimal Money(JsonElement e, string name) => decimal.Parse(e.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static int IntOr(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) ? v.GetInt32() : fallback;
    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static void Same(JsonElement expected, string name, decimal actual) => Assert.Equal(Money(expected, name), actual);

    private static void SameLine(JsonElement expected, PricedLine? actual)
    {
        if (actual is null)
        {
            // a line the golden shows with 0 pieces is a line the cart does not have
            Assert.Equal(0, Int(expected, "qty"));
            Assert.Equal((0m, 0m, 0m), (Money(expected, "line_gross"), Money(expected, "item_promo_discount"), Money(expected, "line_net")));
            return;
        }
        Assert.Equal(Int(expected, "qty"), actual.Qty);
        Same(expected, "unit_price", actual.UnitPrice);
        Same(expected, "line_gross", actual.LineGross);
        Same(expected, "item_promo_discount", actual.ItemPromoDiscount);
        Same(expected, "line_net", actual.LineNet);
    }

    private static List<PricingLine> Cart(params (int Id, int Qty, decimal Price)[] lines) =>
        lines.Where(l => l.Qty > 0).Select(l => new PricingLine(l.Id, l.Qty, l.Price)).ToList();

    private static ItemPromotionPricing SelectOnly(List<PricingLine> cart, ItemPromotion promotion) => Pricing.SelectPromotions(cart, [promotion]);

    // ── CALC-talad-001 · BR-talad-028@v1 — item promotions → whole bill → member, one after the other ─────

    [Theory, MemberData(nameof(Gd001))]
    public void Gd001_the_chain_of_discounts(int i)
    {
        var (input, expected) = Row("GD-talad-001", i);
        var items = input.GetProperty("items").EnumerateArray().Select(x => (Money(x, "unit_price"), Int(x, "qty"), Money(x, "item_promo_discount")));

        var subtotal = Pricing.Subtotal(items);
        var bill = Pricing.Chain(subtotal, Int(input, "bill_rate"), Int(input, "member_rate"));

        Same(expected, "subtotal", bill.Subtotal);
        Same(expected, "bill_discount", bill.BillDiscount);
        Same(expected, "after_bill", bill.AfterBill);
        Same(expected, "member_discount", bill.MemberDiscount);
        Same(expected, "net", bill.Net);
    }

    // ── CALC-talad-002 · BR-talad-027@v1 — HALF_UP at the satang ───────────────────────────────────────

    [Theory, MemberData(nameof(Gd002))]
    public void Gd002_a_percent_rounded_half_up(int i)
    {
        var (input, expected) = Row("GD-talad-002", i);
        var @base = Money(input, "base");

        var discount = Talad.Domain.Sales.Money.PercentOf(@base, Int(input, "rate"));
        var step = Pricing.Chain(@base, Int(input, "rate"), 0);

        Same(expected, "discount", discount);
        Same(expected, "after", step.AfterBill);
    }

    // ── CALC-talad-003 · BR-talad-009@v1 — % per item, and the whole-bill minimum ──────────────────────

    [Theory, MemberData(nameof(Gd003))]
    public void Gd003_percent_per_item_and_the_whole_bill(int i)
    {
        var (input, expected) = Row("GD-talad-003", i);
        if (Str(input, "kind") == "item")
        {
            var price = Money(input, "unit_price");
            var qty = Int(input, "qty");
            var rate = Int(input, "item_rate");
            var line = SelectOnly(Cart((1, qty, price)), new ItemPromotion(1, "ลด %", PromotionType.ItemPercent, 1, null, null, null, null, null, rate)).Lines!.Single();

            Same(expected, "item_pct_discount", PromotionMath.ItemPercent(price, qty, rate));
            Same(expected, "line_gross", line.LineGross);
            Same(expected, "line_net", line.LineNet);
            return;
        }

        var items = input.GetProperty("items").EnumerateArray().ToList();
        var cart = Cart(items.Select((x, n) => (n + 1, Int(x, "qty"), Money(x, "unit_price"))).ToArray());
        var itemPromos = items.Select((x, n) => new ItemPromotion(n + 1, $"ลด {Int(x, "item_rate")}%", PromotionType.ItemPercent, n + 1, null, null, null, null, null, Int(x, "item_rate"))).ToList();
        var billPromos = input.GetProperty("bill_promos").EnumerateArray()
            .Select((p, n) => new BillPromotion(100 + n, $"{Int(p, "rate")}% ขั้นต่ำ {p.GetProperty("min_subtotal").GetString()}", Int(p, "rate"), Money(p, "min_subtotal")))
            .ToList();

        var (_, bill) = Pricing.Price(cart, itemPromos, billPromos, hasMember: false, shopMemberRate: 0);

        Same(expected, "subtotal", bill!.Subtotal);
        Assert.Equal(expected.GetProperty("eligible_promos").EnumerateArray().Select(e => e.GetString()), bill.EligiblePromotions.Select(p => p.Name));
        Assert.Equal(Int(expected, "bill_rate"), bill.BillRate);
        Same(expected, "bill_discount", bill.BillDiscount);
        Same(expected, "after_bill", bill.AfterBill);
    }

    // ── CALC-talad-004 · BR-talad-011@v1 — buy x get y ─────────────────────────────────────────────────

    [Theory, MemberData(nameof(Gd004))]
    public void Gd004_buy_x_get_y(int i)
    {
        var (input, expected) = Row("GD-talad-004", i);
        int x = Int(input, "x"), y = Int(input, "y");

        if (Str(input, "kind") == "same_item")
        {
            var cart = Cart((1, Int(input, "qty_in_cart"), Money(input, "unit_price")));
            var promo = new ItemPromotion(1, "แถม", PromotionType.BuyXGetY, 1, x, null, null, 1, y, null);
            var effect = PromotionMath.Evaluate(promo, cart.ToDictionary(l => l.ProductId));

            Assert.Equal(Int(expected, "free_qty"), effect.FreeQty);
            Assert.Equal(Int(expected, "charged_qty"), effect.ChargedFreeLineQty);
            Same(expected, "free_discount", effect.Discount);
            SameLine(expected.GetProperty("line"), SelectOnly(cart, promo).Lines!.Single());
            return;
        }

        // different items: 1 is bought, 2 is the gift; the gift's price is the one now, whatever it was when picked (price_at_add_free)
        var lines = Cart((1, Int(input, "qty_buy"), Money(input, "unit_price_buy")), (2, Int(input, "qty_free_in_cart"), Money(input, "unit_price_free")));
        var gift = new ItemPromotion(1, "แถม", PromotionType.BuyXGetY, 1, x, null, null, 2, y, null);
        var result = PromotionMath.Evaluate(gift, lines.ToDictionary(l => l.ProductId));
        var priced = SelectOnly(lines, gift).Lines!;

        Assert.Equal((Int(expected, "entitled"), Int(expected, "free_qty")), (result.Entitled, result.FreeQty));
        Assert.Equal(Int(expected, "charged_free_item_qty"), result.ChargedFreeLineQty);
        Same(expected, "free_discount", result.Discount);
        SameLine(expected.GetProperty("buy_line"), priced.SingleOrDefault(l => l.ProductId == 1));
        SameLine(expected.GetProperty("free_line"), priced.SingleOrDefault(l => l.ProductId == 2));
    }

    // ── CALC-talad-005 · BR-talad-012@v1 — buy a + b get y ─────────────────────────────────────────────

    [Theory, MemberData(nameof(Gd005))]
    public void Gd005_buy_a_and_b_get_y(int i)
    {
        var (input, expected) = Row("GD-talad-005", i);
        var kind = Str(input, "kind");
        int na = Int(input, "n_a"), nb = Int(input, "n_b"), y = Int(input, "y");
        // a = 1 · b = 2 · a gift that is neither = 3
        var free = kind switch { "free_is_a" => 1, "free_is_b" => 2, _ => 3 };
        var lines = kind == "free_other"
            ? Cart((1, Int(input, "qty_a"), Money(input, "unit_price_a")), (2, Int(input, "qty_b"), Money(input, "unit_price_b")), (3, Int(input, "qty_free_in_cart"), Money(input, "unit_price_free")))
            : Cart((1, Int(input, "qty_a"), Money(input, "unit_price_a")), (2, Int(input, "qty_b"), Money(input, "unit_price_b")));
        var promo = new ItemPromotion(1, "แถม", PromotionType.BuyAbGetY, 1, na, 2, nb, free, y, null);

        var effect = PromotionMath.Evaluate(promo, lines.ToDictionary(l => l.ProductId));
        var priced = SelectOnly(lines, promo);

        Assert.Equal(Int(expected, "sets"), effect.Sets);
        Assert.Equal(Int(expected, "free_qty"), effect.FreeQty);
        if (kind == "free_other")
        {
            Assert.Equal(Int(expected, "entitled"), effect.Entitled);
            Assert.Equal(Int(expected, "charged_free_item_qty"), effect.ChargedFreeLineQty);
        }
        Same(expected, "free_discount", effect.Discount);
        foreach (var line in expected.GetProperty("lines").EnumerateArray())
        {
            var id = Str(line, "item") switch { "a" => 1, "b" => 2, _ => 3 };
            SameLine(line, priced.Lines!.SingleOrDefault(l => l.ProductId == id));
        }
        Same(expected, "total_gross", priced.Lines!.Sum(l => l.LineGross));
        Same(expected, "promo_discount", priced.PromoDiscount!.Value);
        Same(expected, "total_pay", Pricing.Subtotal(priced.Lines!.Select(l => (l.UnitPrice, l.Qty, l.ItemPromoDiscount))));
    }

    // ── CALC-talad-006 · BR-talad-013@v1 — buy a + b, y% off the pairs ────────────────────────────────────

    [Theory, MemberData(nameof(Gd006))]
    public void Gd006_buy_a_and_b_percent_off(int i)
    {
        var (input, expected) = Row("GD-talad-006", i);
        var lines = Cart((1, Int(input, "qty_a"), Money(input, "unit_price_a")), (2, Int(input, "qty_b"), Money(input, "unit_price_b")));
        var promo = new ItemPromotion(1, "ชุด", PromotionType.BuyAbPercent, 1, Int(input, "n_a"), 2, Int(input, "n_b"), null, null, Int(input, "y"));

        var effect = PromotionMath.Evaluate(promo, lines.ToDictionary(l => l.ProductId));
        var priced = SelectOnly(lines, promo);

        Assert.Equal(Int(expected, "sets"), effect.Sets);
        foreach (var line in expected.GetProperty("lines").EnumerateArray())
        {
            var id = Str(line, "item") == "a" ? 1 : 2;
            Assert.Equal(Int(line, "discounted_qty"), effect.DiscountedQty.GetValueOrDefault(id));
            SameLine(line, priced.Lines!.SingleOrDefault(l => l.ProductId == id));
        }
        Same(expected, "promo_discount", priced.PromoDiscount!.Value);
        Same(expected, "total_gross", priced.Lines!.Sum(l => l.LineGross));
        Same(expected, "total_pay", Pricing.Subtotal(priced.Lines!.Select(l => (l.UnitPrice, l.Qty, l.ItemPromoDiscount))));
    }

    // ── CALC-talad-007 · BR-talad-014@v1 — buy x, y% off each full set ───────────────────────────────────

    [Theory, MemberData(nameof(Gd007))]
    public void Gd007_buy_x_percent_off(int i)
    {
        var (input, expected) = Row("GD-talad-007", i);
        var lines = Cart((1, Int(input, "qty"), Money(input, "unit_price")));
        var promo = new ItemPromotion(1, "ชุด", PromotionType.BuyXPercent, 1, Int(input, "x"), null, null, null, null, Int(input, "y"));

        var effect = PromotionMath.Evaluate(promo, lines.ToDictionary(l => l.ProductId));
        var line = SelectOnly(lines, promo).Lines!.Single();

        Assert.Equal((Int(expected, "sets"), Int(expected, "discounted_qty")), (effect.Sets, effect.DiscountedQty[1]));
        SameLine(expected, line);
    }

    // ── CALC-talad-008 · BR-talad-029@v1 — the promotion that gives most takes its lines, round after round ─

    [Theory, MemberData(nameof(Gd008))]
    public void Gd008_choosing_among_item_promotions(int i)
    {
        var (input, expected) = Row("GD-talad-008", i);
        var products = new Dictionary<string, int>();
        int Id(string name) => products.TryGetValue(name, out var id) ? id : products[name] = products.Count + 1;
        var cart = input.GetProperty("cart").EnumerateArray().Select(l => new PricingLine(Id(Str(l, "product")), Int(l, "qty"), Money(l, "unit_price"))).ToList();
        var keys = new Dictionary<string, int>();
        var promos = input.GetProperty("promos").EnumerateArray().Select(p =>
        {
            var key = keys[Str(p, "id")] = keys.Count + 1;
            var name = Str(p, "name");
            return Str(p, "kind") switch
            {
                "pct" => new ItemPromotion(key, name, PromotionType.ItemPercent, Id(Str(p, "product")), null, null, null, null, null, Int(p, "rate")),
                "x_pct" => new ItemPromotion(key, name, PromotionType.BuyXPercent, Id(Str(p, "product")), Int(p, "x"), null, null, null, null, Int(p, "y")),
                "ab_pct" => new ItemPromotion(key, name, PromotionType.BuyAbPercent, Id(Str(p, "a")), Int(p, "n_a"), Id(Str(p, "b")), Int(p, "n_b"), null, null, Int(p, "y")),
                "xy_diff" => new ItemPromotion(key, name, PromotionType.BuyXGetY, Id(Str(p, "buy")), Int(p, "x"), null, null, Id(Str(p, "free")), Int(p, "y"), null),
                var k => throw new InvalidOperationException($"GD-talad-008 row {i} has a promotion kind {k} this adapter does not map"),
            };
        }).ToList();
        var choice = input.TryGetProperty("staff_choice", out var c) ? c.EnumerateArray().Select(s => keys[s.GetString()!]).ToList() : [];
        string NameOf(int product) => products.Single(kv => kv.Value == product).Key;

        var result = Pricing.SelectPromotions(cart, promos, choice);

        var steps = expected.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(steps.Count, result.Steps.Count);
        foreach (var (want, got) in steps.Zip(result.Steps))
        {
            Assert.Equal(
                want.GetProperty("candidates").EnumerateObject().ToDictionary(p => p.Name, p => decimal.Parse(p.Value.GetString()!, CultureInfo.InvariantCulture)),
                got.Candidates.ToDictionary(x => x.Promotion.Name, x => x.Discount));
            Assert.Equal(
                want.GetProperty("tie").ValueKind == JsonValueKind.Null ? null : want.GetProperty("tie").EnumerateArray().Select(t => t.GetString()!).ToList(),
                got.Tie?.Select(t => t.Name).ToList());
            Assert.Equal(Str(want, "picked"), got.Picked.Name);
            Assert.Equal(want.GetProperty("lines_taken").EnumerateArray().Select(l => l.GetString()), got.LinesTaken.Select(NameOf));
        }

        if (expected.TryGetProperty("needs_staff_choice", out var needs))
        {
            Assert.Equal(needs.EnumerateArray().Select(n => n.GetString()), result.NeedsStaffChoice!.Select(p => p.Name));
            Assert.Null(result.Lines);
            return;
        }
        Assert.Null(result.NeedsStaffChoice);
        var lines = expected.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(lines.Count, result.Lines!.Count);
        foreach (var (want, got) in lines.Zip(result.Lines))
        {
            Assert.Equal(Str(want, "product"), NameOf(got.ProductId));
            Assert.Equal(want.GetProperty("promo").GetString(), got.Promotion?.Name);
            SameLine(want, got);
        }
        Same(expected, "promo_discount", result.PromoDiscount!.Value);

        if (expected.TryGetProperty("bill", out var bill))
        {
            var totals = Pricing.Chain(Pricing.Subtotal(result.Lines.Select(l => (l.UnitPrice, l.Qty, l.ItemPromoDiscount))), IntOr(input, "bill_rate", 0), 0);
            Same(bill, "subtotal", totals.Subtotal);
            Same(bill, "bill_discount", totals.BillDiscount);
            Same(bill, "after_bill", totals.AfterBill);
            Same(bill, "member_discount", totals.MemberDiscount);
            Same(bill, "net", totals.Net);
        }
    }

    // ── CALC-talad-009 · BR-talad-010@v1 — the shop's one member rate, as it is when the bill is priced ──────

    [Theory, MemberData(nameof(Gd009))]
    public void Gd009_the_member_rate(int i)
    {
        var (input, expected) = Row("GD-talad-009", i);
        var items = input.GetProperty("items").EnumerateArray().Select(x => (Money(x, "unit_price"), Int(x, "qty"), Money(x, "item_promo_discount")));

        // rate_at_attach, when given, is what the rate was when the member was bound — it plays no part
        var rate = Pricing.MemberRate(input.GetProperty("has_member").GetBoolean(), Int(input, "shop_member_rate_at_checkout"));
        var bill = Pricing.Chain(Pricing.Subtotal(items), IntOr(input, "bill_rate", 0), rate);

        Assert.Equal(Int(expected, "member_rate"), bill.MemberRate);
        Same(expected, "subtotal", bill.Subtotal);
        Same(expected, "bill_discount", bill.BillDiscount);
        Same(expected, "after_bill", bill.AfterBill);
        Same(expected, "member_discount", bill.MemberDiscount);
        Same(expected, "net", bill.Net);
    }
}
