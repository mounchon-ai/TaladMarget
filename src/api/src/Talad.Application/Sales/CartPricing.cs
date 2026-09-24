using Talad.Application.Members;
using Talad.Application.Promotions;
using Talad.Application.Settings;
using Talad.Domain.Members;
using Talad.Domain.Promotions;
using Talad.Domain.Sales;

namespace Talad.Application.Sales;

public sealed record PromotionRef(int Id, string Name);

/// <summary>One of the tied promotions UI-talad-003 lists — its name and the baht it gives (BR-talad-029@v1).</summary>
public sealed record PromotionChoice(int Id, string Name, decimal Discount);

/// <summary>
/// One line of API-009 — the price in force now, and the item-level promotion that took the line with what it gave
/// this line (BR-talad-016@v2 — a set promotion shows its part under each of its lines). While a choice is pending the
/// discount fields are null: nothing is shown on a guess.
/// </summary>
public sealed record CartPricingLine(
    int ProductId, string Name, int Qty, decimal UnitPrice, decimal LineGross, PromotionRef? Promotion, decimal? ItemPromoDiscount, decimal? LineNet);

/// <summary>
/// API-009 · UI-talad-002's totals: each line, the item promotions, the whole-bill discount, the member discount and what
/// is to be paid (CALC-talad-001). <c>NeedsChoice</c> names the promotions that give exactly the same and wait for the
/// staff to pick one (BR-talad-029@v1); the totals are null until they do. <c>Member</c> is the member whose rate is
/// counted — a member bound and still ACTIVE.
/// </summary>
public sealed record CartPricingView(
    IReadOnlyList<CartPricingLine> Lines,
    decimal? PromoDiscount, decimal? Subtotal,
    PromotionRef? BillPromotion, int? BillRate, decimal? BillDiscount, decimal? AfterBill,
    MemberView? Member, int? MemberRate, decimal? MemberDiscount, decimal? Net,
    IReadOnlyList<PromotionChoice>? NeedsChoice);

/// <summary>
/// UC-talad-004 · API-009 — the caller's own OPEN cart priced now: prices, promotions and the member rate in force at
/// this moment (BR-talad-038@v1), promotions by the Thai calendar date (BR-talad-015@v1). The staff's choices for tied
/// promotions come with the request, one per tied round in order, and are not kept — the answer is worked out afresh
/// each time, so a choice never outlives the prices it was made on. Paying is FE-talad-033's; this reads only.
/// </summary>
/// <summary>
/// One reading of what a cart costs now (UC-talad-004) — the engine's answer plus what a bill must keep of it: the
/// versions in force for each price and promotion, the bill promotion counted and the member-discount version
/// (BR-talad-036@v1). API-009 shows it; API-010 (FE-talad-033) pays it — the same reading, so they cannot disagree.
/// </summary>
public sealed record CartQuote(
    IReadOnlyList<PricingLine> Lines,
    IReadOnlyDictionary<int, string> Names,
    ItemPromotionPricing Items,
    BillTotals? Bill,
    Member? Member,
    BillPromotion? BillPromotion,
    IReadOnlyDictionary<int, int> PriceVersions,
    IReadOnlyDictionary<int, int> PromotionVersions,
    int? MemberDiscountVersionId);

public sealed class CartPricing(ICartRepository carts, IPromotionRepository promotions, IMemberDiscountRepository memberDiscounts, TimeProvider clock)
{
    /// <summary>Asia/Bangkok is +07:00 all year (no daylight saving) — the date a promotion's days are counted in.</summary>
    public static DateOnly ThaiToday(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);

    public async Task<CartPricingView> PriceAsync(int ownerId, IReadOnlyList<int> staffChoice, CancellationToken ct = default)
    {
        var quote = await QuoteAsync(await carts.FindOpenAsync(ownerId, ct), staffChoice, clock.GetUtcNow(), ct);
        var member = quote.Member is null ? null : MemberView.Of(quote.Member);

        if (quote.Items.NeedsStaffChoice is not null)
        {
            var plain = quote.Lines.Select(l => new CartPricingLine(l.ProductId, quote.Names[l.ProductId], l.Qty, l.UnitPrice, l.Gross, null, null, null)).ToList();
            return new CartPricingView(plain, null, null, null, null, null, null, member, null, null, null, ChoicesOf(quote));
        }

        var bill = quote.Bill!;
        var priced = quote.Items.Lines!.Select(l => new CartPricingLine(
            l.ProductId, quote.Names[l.ProductId], l.Qty, l.UnitPrice, l.LineGross, l.Promotion is { } p ? Ref(p) : null, l.ItemPromoDiscount, l.LineNet)).ToList();
        return new CartPricingView(
            priced, quote.Items.PromoDiscount, bill.Subtotal,
            quote.BillPromotion is { } b ? new PromotionRef(b.Id, b.Name) : null, bill.BillRate, bill.BillDiscount, bill.AfterBill,
            member, bill.MemberRate, bill.MemberDiscount, bill.Net,
            null);
    }

    /// <summary>The tied promotions UI-talad-003 lists, each with the baht it gives.</summary>
    public static IReadOnlyList<PromotionChoice> ChoicesOf(CartQuote quote) =>
        quote.Items.NeedsStaffChoice!.Select(p => new PromotionChoice(p.Id, p.Name, quote.Items.TieDiscount!.Value)).ToList();

    /// <summary>
    /// <paramref name="cart"/> priced at <paramref name="now"/>: its lines at the prices in force, the promotions in force
    /// on the Thai date, the member rate in force, for a member bound and still ACTIVE. A missing cart prices to nothing.
    /// </summary>
    public async Task<CartQuote> QuoteAsync(Cart? cart, IReadOnlyList<int> staffChoice, DateTimeOffset now, CancellationToken ct = default)
    {
        var inForce = await promotions.InForceAsync(ThaiToday(now), ct);
        var rate = await memberDiscounts.LatestAsync(ct);
        var shopRate = rate is var (version, _) ? version.RatePercent : 0;

        var cartLines = cart?.Lines.OrderBy(l => l.AddedAt).ToList() ?? [];
        var member = cart?.Member is { Status: MemberStatus.Active } m ? m : null;
        var itemPromotions = inForce.Where(p => p.CurrentVersion!.Type != PromotionType.BillPercent).Select(ToItem).ToList();
        var billPromotions = inForce.Where(p => p.CurrentVersion!.Type == PromotionType.BillPercent)
            .Select(p => new BillPromotion(p.Id, p.CurrentVersion!.Name, p.CurrentVersion.RatePercent ?? 0, p.CurrentVersion.MinSubtotal ?? 0m))
            .ToList();

        var lines = cartLines.Select(l => new PricingLine(l.ProductId, l.Qty, l.Product.CurrentPrice)).ToList();
        var (items, bill) = Pricing.Price(lines, itemPromotions, billPromotions, member is not null, shopRate, staffChoice);
        var counted = bill is null || bill.BillRate == 0 ? null : bill.EligiblePromotions.Where(p => p.Rate == bill.BillRate).OrderBy(p => p.Id).First();

        return new CartQuote(
            lines,
            cartLines.ToDictionary(l => l.ProductId, l => l.Product.Name),
            items,
            bill,
            member,
            counted,
            cartLines.ToDictionary(l => l.ProductId, l => l.Product.CurrentPriceVersionId!.Value),
            inForce.ToDictionary(p => p.Id, p => p.CurrentVersionId!.Value),
            rate?.Version.Id);
    }

    private static PromotionRef Ref(ItemPromotion p) => new(p.Id, p.Name);

    private static ItemPromotion ToItem(Promotion p)
    {
        var v = p.CurrentVersion!;
        return new ItemPromotion(p.Id, v.Name, v.Type, v.ProductAId!.Value, v.QtyA, v.ProductBId, v.QtyB, v.FreeProductId, v.FreeQty, v.RatePercent);
    }
}
