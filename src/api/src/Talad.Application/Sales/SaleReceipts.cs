using Talad.Application.Members;
using Talad.Domain.Members;
using Talad.Domain.Sales;

namespace Talad.Application.Sales;

/// <summary>
/// A bill as it was kept, with what it points at: the seller's name, the member (hidden or not — BR-talad-040@v2), each
/// product's name (discontinued or not), each promotion under the name of the VERSION the bill kept (BR-talad-036@v1)
/// keyed by version id, and the member rate of the version it kept (0 when none, as <see cref="Pricing.MemberRate"/>).
/// </summary>
public sealed record SaleReading(
    Sale Sale, string SellerName, Member? Member, IReadOnlyDictionary<int, string> ProductNames,
    IReadOnlyDictionary<int, PromotionRef> PromotionVersions, int MemberRate);

public interface ISaleReader
{
    /// <summary>The bill <paramref name="saleId"/> if <paramref name="sellerId"/> sold it — anyone else's is null (BR-talad-020@v1).</summary>
    Task<SaleReading?> FindOwnAsync(int saleId, int sellerId, CancellationToken ct);
}

/// <summary>The bill is not the caller's own, or there is none — someone else's is as if it were not there.</summary>
public sealed class SaleNotFoundException(int saleId) : Exception($"sale {saleId} is not the caller's");

/// <summary>
/// UC-talad-005 · API-011 — the receipt of a bill the caller sold (ACL-005; the owner sells from their own cart and
/// inherits it). Read back from what the bill kept, never re-priced: a price, promotion or member rate changed since —
/// or a promotion discontinued, a member hidden — does not reach it (BR-talad-035@v1 · BR-talad-036@v1). The same shape
/// API-010 answers. Every bill the owner may open (ACL-013) and the sales history are FE-talad-037's.
/// </summary>
public sealed class SaleReceipts(ISaleReader sales)
{
    public async Task<SaleView> GetOwnAsync(int saleId, int viewerId, CancellationToken ct = default)
    {
        var read = await sales.FindOwnAsync(saleId, viewerId, ct) ?? throw new SaleNotFoundException(saleId);
        var sale = read.Sale;
        PromotionRef? Promotion(int? versionId) => versionId is { } v ? read.PromotionVersions[v] : null;
        return new SaleView(
            sale.Id, sale.ReceiptNo, sale.PaidAt, sale.SellerId, read.SellerName, read.Member is { } m ? MemberView.Of(m) : null,
            sale.Lines.OrderBy(l => l.LineNo).Select(l => new SaleLineView(
                l.LineNo, l.ProductId, read.ProductNames[l.ProductId], l.Qty, l.FreeQty, l.UnitPrice,
                Promotion(l.PromotionVersionId), l.PromoDiscount, l.LineNet)).ToList(),
            sale.Subtotal, sale.PromoDiscountTotal, Promotion(sale.BillPromotionVersionId), sale.BillDiscount,
            read.MemberRate, sale.MemberDiscount, sale.NetTotal, sale.Status.ToString().ToUpperInvariant());
    }
}
