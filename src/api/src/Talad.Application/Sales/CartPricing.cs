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
public sealed class CartPricing(ICartRepository carts, IPromotionRepository promotions, IMemberDiscountRepository memberDiscounts, TimeProvider clock)
{
    /// <summary>Asia/Bangkok is +07:00 all year (no daylight saving) — the date a promotion's days are counted in.</summary>
    public static DateOnly ThaiToday(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);

    public async Task<CartPricingView> PriceAsync(int ownerId, IReadOnlyList<int> staffChoice, CancellationToken ct = default)
    {
        var cart = await carts.FindOpenAsync(ownerId, ct);
        var inForce = await promotions.InForceAsync(ThaiToday(clock.GetUtcNow()), ct);
        var shopRate = await memberDiscounts.LatestAsync(ct) is var (version, _) ? version.RatePercent : 0;

        var cartLines = cart?.Lines.OrderBy(l => l.AddedAt).ToList() ?? [];
        var names = cartLines.ToDictionary(l => l.ProductId, l => l.Product.Name);
        var member = cart?.Member is { Status: MemberStatus.Active } m ? m : null;
        var itemPromotions = inForce.Where(p => p.CurrentVersion!.Type != PromotionType.BillPercent).Select(ToItem).ToList();
        var billPromotions = inForce.Where(p => p.CurrentVersion!.Type == PromotionType.BillPercent)
            .Select(p => new BillPromotion(p.Id, p.CurrentVersion!.Name, p.CurrentVersion.RatePercent ?? 0, p.CurrentVersion.MinSubtotal ?? 0m))
            .ToList();

        var lines = cartLines.Select(l => new PricingLine(l.ProductId, l.Qty, l.Product.CurrentPrice)).ToList();
        var (items, bill) = Pricing.Price(lines, itemPromotions, billPromotions, member is not null, shopRate, staffChoice);

        if (items.NeedsStaffChoice is { } tie)
        {
            var plain = lines.Select(l => new CartPricingLine(l.ProductId, names[l.ProductId], l.Qty, l.UnitPrice, l.Gross, null, null, null)).ToList();
            return new CartPricingView(plain, null, null, null, null, null, null, member is null ? null : MemberView.Of(member), null, null, null,
                tie.Select(p => new PromotionChoice(p.Id, p.Name, items.TieDiscount!.Value)).ToList());
        }

        var priced = items.Lines!.Select(l => new CartPricingLine(
            l.ProductId, names[l.ProductId], l.Qty, l.UnitPrice, l.LineGross, l.Promotion is { } p ? Ref(p) : null, l.ItemPromoDiscount, l.LineNet)).ToList();
        var counted = bill!.EligiblePromotions.Where(p => p.Rate == bill.BillRate).OrderBy(p => p.Id).FirstOrDefault();
        return new CartPricingView(
            priced, items.PromoDiscount, bill.Subtotal,
            counted is null || bill.BillRate == 0 ? null : new PromotionRef(counted.Id, counted.Name), bill.BillRate, bill.BillDiscount, bill.AfterBill,
            member is null ? null : MemberView.Of(member), bill.MemberRate, bill.MemberDiscount, bill.Net,
            null);
    }

    private static PromotionRef Ref(ItemPromotion p) => new(p.Id, p.Name);

    private static ItemPromotion ToItem(Promotion p)
    {
        var v = p.CurrentVersion!;
        return new ItemPromotion(p.Id, v.Name, v.Type, v.ProductAId!.Value, v.QtyA, v.ProductBId, v.QtyB, v.FreeProductId, v.FreeQty, v.RatePercent);
    }
}
