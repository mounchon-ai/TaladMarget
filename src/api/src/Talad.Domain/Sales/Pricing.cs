using Talad.Domain.Promotions;

namespace Talad.Domain.Sales;

// UC-talad-004 · the arithmetic of a cart, as req's calculation contracts write it (CALC-talad-001..009). Nothing here
// reads a database or a clock: the caller hands in the prices, the promotions and the member rate in force at the
// moment it prices (BR-talad-038@v1), so the same inputs give the same answer, and the golden rows can be fed in
// as they are.

/// <summary>CALC-talad-002 — the one rounding every discount uses.</summary>
public static class Money
{
    /// <summary>HALF_UP at 0.01 baht — 9.045 → 9.05, not .NET's default banker's rounding (which gives 9.04).</summary>
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>CALC-talad-002 · discount = round_HALF_UP(base × rate / 100, 0.01), rounded as soon as it is worked out.</summary>
    public static decimal PercentOf(decimal @base, int rate) => Round(@base * rate / 100m);
}

/// <summary>One cart line as it is priced: the price in force now, not the one when it was added (BR-talad-038@v1).</summary>
public sealed record PricingLine(int ProductId, int Qty, decimal UnitPrice)
{
    public decimal Gross => UnitPrice * Qty;
}

/// <summary>
/// An item-level promotion in force (every ENT-006 type but BILL_PERCENT), with the parameters of its type:
/// ITEM_PERCENT (A, rate) · BUY_X_PERCENT (A, x = QtyA, y = rate) · BUY_AB_PERCENT (A, B, n_a = QtyA, n_b = QtyB,
/// y = rate) · BUY_X_GET_Y (buy = A, x = QtyA, free = FreeProduct, y = FreeQty) · BUY_AB_GET_Y (A, B, n_a, n_b,
/// free = FreeProduct, y = FreeQty).
/// </summary>
public sealed record ItemPromotion(
    int Id, string Name, PromotionType Type, int ProductA, int? QtyA, int? ProductB, int? QtyB, int? FreeProduct, int? FreeQty, int? Rate);

/// <summary>A BILL_PERCENT promotion in force — its rate and the subtotal it needs (CALC-talad-003).</summary>
public sealed record BillPromotion(int Id, string Name, int Rate, decimal MinSubtotal);

/// <summary>
/// What one promotion would give from the lines offered to it (CALC-talad-003..007, each rounded at its own point).
/// <see cref="Lines"/> are the lines it takes whole if picked (BR-talad-029@v1), a line with no discount of its own
/// included — the bought line of a buy-and-get-free. <see cref="FreeQty"/> and <see cref="ChargedFreeLineQty"/> are the
/// free-gift types' alone: pieces given free, and pieces of the free product's line still paid for.
/// </summary>
public sealed record PromotionEffect(
    decimal Discount,
    IReadOnlyList<int> Lines,
    IReadOnlyDictionary<int, decimal> LineDiscounts,
    IReadOnlyDictionary<int, int> DiscountedQty,
    int Sets,
    int? Entitled,
    int? FreeQty,
    int? ChargedFreeLineQty);

/// <summary>CALC-talad-003..007 — one formula per promotion type.</summary>
public static class PromotionMath
{
    /// <summary>CALC-talad-003 · per item — item_pct_discount = round_HALF_UP(unit_price × qty × rate / 100), once per line.</summary>
    public static decimal ItemPercent(decimal unitPrice, int qty, int rate) => Money.PercentOf(unitPrice * qty, rate);

    /// <summary>The effect of <paramref name="p"/> on <paramref name="lines"/>; a line it needs that is not offered counts as 0 pieces.</summary>
    public static PromotionEffect Evaluate(ItemPromotion p, IReadOnlyDictionary<int, PricingLine> lines)
    {
        PricingLine? Line(int? id) => id is { } i && lines.TryGetValue(i, out var l) ? l : null;
        static int Qty(PricingLine? l) => l?.Qty ?? 0;
        var a = Line(p.ProductA);
        var rate = p.Rate ?? 0;

        switch (p.Type)
        {
            case PromotionType.ItemPercent:
            {
                if (a is null) return Nothing();
                var disc = ItemPercent(a.UnitPrice, a.Qty, rate);
                return Effect(disc, [a], new() { [a.ProductId] = disc }, new() { [a.ProductId] = a.Qty }, sets: a.Qty);
            }
            case PromotionType.BuyXPercent: // CALC-talad-007
            {
                if (a is null) return Nothing();
                var x = p.QtyA!.Value;
                var sets = a.Qty / x;
                var disc = Money.Round(sets * x * a.UnitPrice * rate / 100m);
                return Effect(disc, [a], new() { [a.ProductId] = disc }, new() { [a.ProductId] = sets * x }, sets);
            }
            case PromotionType.BuyAbPercent: // CALC-talad-006 — each line rounded on its own
            {
                var b = Line(p.ProductB);
                int na = p.QtyA!.Value, nb = p.QtyB!.Value;
                var sets = Math.Min(Qty(a) / na, Qty(b) / nb);
                var discs = new Dictionary<int, decimal>();
                var counted = new Dictionary<int, int>();
                if (a is not null) { discs[a.ProductId] = Money.Round(sets * na * a.UnitPrice * rate / 100m); counted[a.ProductId] = sets * na; }
                if (b is not null) { discs[b.ProductId] = Money.Round(sets * nb * b.UnitPrice * rate / 100m); counted[b.ProductId] = sets * nb; }
                return Effect(discs.Values.Sum(), Present(a, b), discs, counted, sets);
            }
            case PromotionType.BuyXGetY: // CALC-talad-004
            {
                int x = p.QtyA!.Value, y = p.FreeQty!.Value;
                if (p.FreeProduct == p.ProductA)
                {
                    if (a is null) return Nothing();
                    var sets = a.Qty / (x + y);
                    var freeQty = sets * y;
                    var disc = freeQty * a.UnitPrice;
                    return Effect(disc, [a], new() { [a.ProductId] = disc }, new() { [a.ProductId] = freeQty }, sets, freeQty: freeQty, charged: a.Qty - freeQty);
                }
                var free = Line(p.FreeProduct);
                var bought = Qty(a) / x;
                var entitled = bought * y;
                var given = Math.Min(Qty(free), entitled);
                var value = free is null ? 0m : given * free.UnitPrice;
                return FreeGift(value, Present(a, free), free, bought, entitled, given);
            }
            case PromotionType.BuyAbGetY: // CALC-talad-005
            {
                var b = Line(p.ProductB);
                int na = p.QtyA!.Value, nb = p.QtyB!.Value, y = p.FreeQty!.Value;
                if (p.FreeProduct == p.ProductA || p.FreeProduct == p.ProductB)
                {
                    // the gift is one of the pair: its free pieces are not counted as bought
                    var giftIsA = p.FreeProduct == p.ProductA;
                    var sets = giftIsA ? Math.Min(Qty(a) / (na + y), Qty(b) / nb) : Math.Min(Qty(a) / na, Qty(b) / (nb + y));
                    var freeQty = sets * y;
                    var gift = giftIsA ? a : b;
                    var disc = gift is null ? 0m : freeQty * gift.UnitPrice;
                    var discs = gift is null ? new Dictionary<int, decimal>() : new() { [gift.ProductId] = disc };
                    var counted = gift is null ? new Dictionary<int, int>() : new() { [gift.ProductId] = freeQty };
                    return Effect(disc, Present(a, b), discs, counted, sets, freeQty: freeQty);
                }
                var other = Line(p.FreeProduct);
                var pairs = Math.Min(Qty(a) / na, Qty(b) / nb);
                var entitled = pairs * y;
                var given = Math.Min(Qty(other), entitled);
                var value = other is null ? 0m : given * other.UnitPrice;
                return FreeGift(value, Present(a, b, other), other, pairs, entitled, given);
            }
            default:
                throw new ArgumentException($"{p.Type} is not an item-level promotion", nameof(p));
        }

        static PromotionEffect Nothing() => new(0m, [], new Dictionary<int, decimal>(), new Dictionary<int, int>(), 0, null, null, null);

        static PromotionEffect Effect(
            decimal discount, IReadOnlyList<PricingLine> taken, Dictionary<int, decimal> discs, Dictionary<int, int> counted, int sets,
            int? freeQty = null, int? charged = null) =>
            new(discount, taken.Select(l => l.ProductId).ToList(), discs, counted, sets, null, freeQty, charged);

        static PromotionEffect FreeGift(decimal value, IReadOnlyList<PricingLine> taken, PricingLine? gift, int sets, int entitled, int given)
        {
            var discs = gift is null ? new Dictionary<int, decimal>() : new() { [gift.ProductId] = value };
            var counted = gift is null ? new Dictionary<int, int>() : new() { [gift.ProductId] = given };
            return new(value, taken.Select(l => l.ProductId).ToList(), discs, counted, sets, entitled, given, (gift?.Qty ?? 0) - given);
        }

        static List<PricingLine> Present(params PricingLine?[] ls) => ls.OfType<PricingLine>().DistinctBy(l => l.ProductId).ToList();
    }
}

/// <summary>One round of CALC-talad-008: every promotion that would give something, the tie if the best are equal, and the pick.</summary>
public sealed record SelectionStep(
    IReadOnlyList<(ItemPromotion Promotion, decimal Discount)> Candidates,
    IReadOnlyList<ItemPromotion>? Tie,
    ItemPromotion Picked,
    IReadOnlyList<int> LinesTaken);

/// <summary>
/// A line after the item-level promotions — the promotion that took it (null when none did), what it gave this line, and
/// how many of its pieces were given free by a buy-and-get promotion (ENT-012.freeQty · CALC-talad-004 · 005).
/// </summary>
public sealed record PricedLine(
    int ProductId, int Qty, decimal UnitPrice, ItemPromotion? Promotion, decimal LineGross, decimal ItemPromoDiscount, decimal LineNet, int FreeQty = 0);

/// <summary>
/// CALC-talad-008's answer. While the best promotions of a round are equal and the staff has not chosen among them,
/// <see cref="NeedsStaffChoice"/> names them, <see cref="TieDiscount"/> is what each of them gives (the same, by
/// definition — UI-talad-003 shows it beside each name), and there are no lines yet — nothing is worked out on a guess.
/// </summary>
public sealed record ItemPromotionPricing(
    IReadOnlyList<SelectionStep> Steps, IReadOnlyList<PricedLine>? Lines, decimal? PromoDiscount, IReadOnlyList<ItemPromotion>? NeedsStaffChoice,
    decimal? TieDiscount = null);

/// <summary>CALC-talad-001 · 003 · 009 — after the item-level promotions: the whole-bill discount, then the member's, one after the other.</summary>
public sealed record BillTotals(
    decimal Subtotal, IReadOnlyList<BillPromotion> EligiblePromotions, int BillRate, decimal BillDiscount, decimal AfterBill,
    int MemberRate, decimal MemberDiscount, decimal Net);

public static class Pricing
{
    /// <summary>
    /// CALC-talad-008 · BR-talad-029@v1 — round after round, the promotion that gives most on the lines still free takes
    /// its lines whole; a promotion that gives 0 is no candidate. Equal best: the staff's choice for that round, in
    /// order, by promotion id — none, or one that is not among the equal best, and the answer is the question.
    /// </summary>
    public static ItemPromotionPricing SelectPromotions(IReadOnlyList<PricingLine> cart, IReadOnlyList<ItemPromotion> promotions, IReadOnlyList<int>? staffChoice = null)
    {
        var free = cart.ToDictionary(l => l.ProductId);
        var takenBy = new Dictionary<int, (ItemPromotion Promotion, decimal Discount, int Free)>();
        var steps = new List<SelectionStep>();
        var choices = new Queue<int>(staffChoice ?? []);

        while (true)
        {
            var candidates = promotions
                .Select(p => (Promotion: p, Effect: PromotionMath.Evaluate(p, free)))
                .Where(c => c.Effect.Discount > 0)
                .ToList();
            if (candidates.Count == 0) break;

            var best = candidates.Max(c => c.Effect.Discount);
            var top = candidates.Where(c => c.Effect.Discount == best).ToList();
            var pick = top[0];
            IReadOnlyList<ItemPromotion>? tie = null;
            if (top.Count > 1)
            {
                tie = top.Select(c => c.Promotion).ToList();
                var at = choices.TryDequeue(out var chosen) ? top.FindIndex(c => c.Promotion.Id == chosen) : -1;
                if (at < 0) return new ItemPromotionPricing(steps, null, null, tie, best);
                pick = top[at];
            }

            foreach (var line in pick.Effect.Lines)
            {
                var gift = pick.Promotion.Type is PromotionType.BuyXGetY or PromotionType.BuyAbGetY;
                takenBy[line] = (pick.Promotion, pick.Effect.LineDiscounts.GetValueOrDefault(line), gift ? pick.Effect.DiscountedQty.GetValueOrDefault(line) : 0);
                free.Remove(line);
            }
            steps.Add(new SelectionStep(candidates.Select(c => (c.Promotion, c.Effect.Discount)).ToList(), tie, pick.Promotion, pick.Effect.Lines));
        }

        var lines = cart.Select(l =>
        {
            var (promotion, discount, free) = takenBy.TryGetValue(l.ProductId, out var t) ? t : (null, 0m, 0);
            return new PricedLine(l.ProductId, l.Qty, l.UnitPrice, promotion, l.Gross, discount, l.Gross - discount, free);
        }).ToList();
        return new ItemPromotionPricing(steps, lines, lines.Sum(l => l.ItemPromoDiscount), null);
    }

    /// <summary>CALC-talad-001 · subtotal = Σ (unit_price × qty − item_promo_discount).</summary>
    public static decimal Subtotal(IEnumerable<(decimal UnitPrice, int Qty, decimal ItemPromoDiscount)> lines) =>
        lines.Sum(l => l.UnitPrice * l.Qty - l.ItemPromoDiscount);

    /// <summary>CALC-talad-003 · whole bill — the highest rate among the promotions whose minimum the subtotal reaches (≥), or 0.</summary>
    public static (IReadOnlyList<BillPromotion> Eligible, int Rate) BillRate(decimal subtotal, IReadOnlyList<BillPromotion> promotions)
    {
        var eligible = promotions.Where(p => subtotal >= p.MinSubtotal).ToList();
        return (eligible, eligible.Count == 0 ? 0 : eligible.Max(p => p.Rate));
    }

    /// <summary>CALC-talad-009 · the shop's one rate, at the moment of pricing, for a bill bound to a member — 0 otherwise.</summary>
    public static int MemberRate(bool hasMember, int shopMemberRate) => hasMember ? shopMemberRate : 0;

    /// <summary>CALC-talad-001 · bill discount, then member discount, each on what the step before left — never % added together.</summary>
    public static BillTotals Chain(decimal subtotal, int billRate, int memberRate, IReadOnlyList<BillPromotion>? eligible = null)
    {
        var billDiscount = Money.PercentOf(subtotal, billRate);
        var afterBill = subtotal - billDiscount;
        var memberDiscount = Money.PercentOf(afterBill, memberRate);
        return new BillTotals(subtotal, eligible ?? [], billRate, billDiscount, afterBill, memberRate, memberDiscount, afterBill - memberDiscount);
    }

    /// <summary>UC-talad-004 — the whole cart: item-level promotions (CALC-talad-008), then the bill and the member (CALC-talad-001 · 003 · 009).</summary>
    public static (ItemPromotionPricing Items, BillTotals? Bill) Price(
        IReadOnlyList<PricingLine> cart, IReadOnlyList<ItemPromotion> itemPromotions, IReadOnlyList<BillPromotion> billPromotions,
        bool hasMember, int shopMemberRate, IReadOnlyList<int>? staffChoice = null)
    {
        var items = SelectPromotions(cart, itemPromotions, staffChoice);
        if (items.Lines is not { } lines) return (items, null);
        var subtotal = Subtotal(lines.Select(l => (l.UnitPrice, l.Qty, l.ItemPromoDiscount)));
        var (eligible, billRate) = BillRate(subtotal, billPromotions);
        return (items, Chain(subtotal, billRate, MemberRate(hasMember, shopMemberRate), eligible));
    }
}
