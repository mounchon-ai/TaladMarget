using Talad.Application.Auth;
using Talad.Application.Members;
using Talad.Domain.Sales;

namespace Talad.Application.Sales;

public interface ISaleRepository
{
    void Add(Sale sale);

    /// <summary>A second bill for the same cart, saved in a race, is a <see cref="ConcurrentUpdateException"/> (BR-talad-039@v1 at db).</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>A line of the bill as it was paid — the price, the promotion and its discount, the free pieces (BR-talad-016@v2).</summary>
public sealed record SaleLineView(
    int LineNo, int ProductId, string Name, int Qty, int FreeQty, decimal UnitPrice, PromotionRef? Promotion, decimal PromoDiscount, decimal LineNet);

/// <summary>API-010 — the bill just made, as the receipt (UC-talad-005) shows it.</summary>
public sealed record SaleView(
    int Id, string ReceiptNo, DateTimeOffset PaidAt, int SellerId, string SellerName, MemberView? Member,
    IReadOnlyList<SaleLineView> Lines, decimal Subtotal, decimal PromoDiscountTotal, PromotionRef? BillPromotion, decimal BillDiscount,
    int MemberRate, decimal MemberDiscount, decimal NetTotal, string Status);

/// <summary>The cart named is not the caller's own OPEN or PAID cart — someone else's is as if it were not there (BR-talad-020@v1).</summary>
public sealed class CartNotFoundException(int cartId) : Exception($"cart {cartId} is not the caller's");

/// <summary>BR-talad-029@v1 — promotions give exactly the same and the staff has not chosen: nothing is paid on a guess.</summary>
public sealed class PromotionChoiceNeededException(IReadOnlyList<PromotionChoice> choices) : Exception("the staff has to choose a promotion")
{
    public IReadOnlyList<PromotionChoice> Choices { get; } = choices;
}

/// <summary>
/// UC-talad-003 · API-010 — the caller pays their own cart (ACL-003). The request names the cart the screen showed, so
/// pressing ชำระเงิน twice, or sending again after the line dropped, finds that cart PAID and is refused
/// (BR-talad-039@v1). Priced by the same reading API-009 shows (<see cref="CartPricing.QuoteAsync"/>), at this moment
/// (BR-talad-038@v1). The bill, the stock it takes, the member's accumulated amount and the cart's PAID are one save;
/// a save that loses a race to another sale or adjustment is run again from a fresh read.
/// </summary>
public sealed class Checkout(ICartRepository carts, ISaleRepository sales, CartPricing pricing, IUserAccountRepository accounts, IUnitOfWork work, TimeProvider clock)
{
    public Task<SaleView> PayAsync(int ownerId, int? cartId, IReadOnlyList<int> staffChoice, CancellationToken ct = default) =>
        Conflicts.RetryAsync(work, async () =>
        {
            var cart = cartId is { } id ? await carts.FindOwnAsync(id, ownerId, ct) : null;
            if (cart is null) throw new CartNotFoundException(cartId ?? 0);
            cart.EnsurePayable(); // paid · empty · no longer sold · not enough left · member hidden — before any choice is asked

            var now = clock.GetUtcNow();
            var quote = await pricing.QuoteAsync(cart, staffChoice, now, ct);
            if (quote.Items.NeedsStaffChoice is not null) throw new PromotionChoiceNeededException(CartPricing.ChoicesOf(quote));

            var billVersion = quote.BillPromotion is { } b ? quote.PromotionVersions[b.Id] : (int?)null;
            var sale = Sale.Pay(cart, quote.Items.Lines!, quote.Bill!, quote.PriceVersions, quote.PromotionVersions, billVersion, quote.MemberDiscountVersionId, now);
            sales.Add(sale);
            await sales.SaveChangesAsync(ct);

            var seller = await accounts.FindByIdAsync(ownerId, ct);
            var promotions = quote.Items.Lines!.Where(l => l.Promotion is not null).ToDictionary(l => l.ProductId, l => l.Promotion!);
            return new SaleView(
                sale.Id, sale.ReceiptNo, sale.PaidAt, sale.SellerId, seller?.DisplayName ?? "", quote.Member is { } m ? MemberView.Of(m) : null,
                sale.Lines.Select(l => new SaleLineView(
                    l.LineNo, l.ProductId, quote.Names[l.ProductId], l.Qty, l.FreeQty, l.UnitPrice,
                    promotions.TryGetValue(l.ProductId, out var p) ? new PromotionRef(p.Id, p.Name) : null, l.PromoDiscount, l.LineNet)).ToList(),
                sale.Subtotal, sale.PromoDiscountTotal, quote.BillPromotion is { } bp ? new PromotionRef(bp.Id, bp.Name) : null, sale.BillDiscount,
                quote.Bill!.MemberRate, sale.MemberDiscount, sale.NetTotal, sale.Status.ToString().ToUpperInvariant());
        });
}
