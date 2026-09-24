namespace Talad.Domain.Sales;

/// <summary>
/// ENT-011 · บิลขาย. Made once, from one cart, when it is paid (BR-talad-039@v1 — the cart is the key that stops a
/// second bill); it keeps the versions of every price, promotion and member rate it was priced with, so nothing a later
/// change does reaches it (BR-talad-035@v1 · BR-talad-036@v1). Its lines are never edited (BR-talad-025@v1) — only the
/// void fields change, and that is a later unit's (STM-talad-004).
/// </summary>
public class Sale
{
    private readonly List<SaleLine> _lines = [];

    public int Id { get; private set; }
    public string ReceiptNo { get; private set; } = null!;
    public int CartId { get; private set; }
    public int SellerId { get; private set; }
    public int? MemberId { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal PromoDiscountTotal { get; private set; }
    public int? BillPromotionVersionId { get; private set; }
    public decimal BillDiscount { get; private set; }
    public int? MemberDiscountVersionId { get; private set; }
    public decimal MemberDiscount { get; private set; }
    public decimal NetTotal { get; private set; }
    public SaleStatus Status { get; private set; }
    public int? VoidedById { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }
    public IReadOnlyList<SaleLine> Lines => _lines;

    private Sale() { } // EF

    /// <summary>
    /// UC-talad-003 — the bill of <paramref name="cart"/>, priced by <paramref name="lines"/> and <paramref name="bill"/>
    /// at <paramref name="paidAt"/>. In one step: the cart is asked whether it can be paid (and refuses in the person's
    /// words if not), every line's pieces — free ones too — leave the stock (BR-talad-007@v1), the bound member
    /// accumulates what was really paid (BR-talad-003@v1), and the cart becomes PAID. The seller is the cart's owner,
    /// never anyone the request names (BR-talad-006@v1).
    /// </summary>
    public static Sale Pay(
        Cart cart, IReadOnlyList<PricedLine> lines, BillTotals bill, IReadOnlyDictionary<int, int> priceVersions,
        IReadOnlyDictionary<int, int> promotionVersions, int? billPromotionVersionId, int? memberDiscountVersionId, DateTimeOffset paidAt)
    {
        cart.EnsurePayable();
        if (lines.Count != cart.Lines.Count || lines.Any(l => cart.Lines.All(c => c.ProductId != l.ProductId || c.Qty != l.Qty)))
            throw new ArgumentException("the priced lines are not this cart's", nameof(lines));

        var sale = new Sale
        {
            ReceiptNo = ReceiptNumber(cart.Id, paidAt),
            CartId = cart.Id,
            SellerId = cart.OwnerId,
            MemberId = cart.MemberId,
            PaidAt = paidAt,
            Subtotal = bill.Subtotal,
            PromoDiscountTotal = lines.Sum(l => l.ItemPromoDiscount),
            BillPromotionVersionId = bill.BillDiscount > 0 ? billPromotionVersionId : null,
            BillDiscount = bill.BillDiscount,
            MemberDiscountVersionId = cart.MemberId is null ? null : memberDiscountVersionId,
            MemberDiscount = bill.MemberDiscount,
            NetTotal = bill.Net,
            Status = SaleStatus.Paid,
        };
        var no = 0;
        foreach (var line in lines)
        {
            sale._lines.Add(new SaleLine(
                ++no, line.ProductId, priceVersions[line.ProductId], line.UnitPrice, line.Qty, line.FreeQty,
                line.Promotion is { } p ? promotionVersions[p.Id] : null, line.ItemPromoDiscount, line.LineNet));
        }

        foreach (var held in cart.Lines) held.Product.RemoveSold(held.Qty);
        cart.Member?.Accumulate(bill.Net);
        cart.MarkPaid();
        return sale;
    }

    /// <summary>
    /// BR-talad-026@v1 — issued by the domain, never reused. Design gives no format, so this one is dev's (FE-talad-033):
    /// the Thai calendar date it was paid, then the cart's number — one cart makes one bill, so it cannot repeat.
    /// </summary>
    public static string ReceiptNumber(int cartId, DateTimeOffset paidAt) =>
        $"{paidAt.ToOffset(TimeSpan.FromHours(7)):yyyyMMdd}-{cartId:D6}";
}

/// <summary>ENT-012 · บรรทัดของบิล — keyed by the bill and its line number; written once (BR-talad-025@v1).</summary>
public class SaleLine
{
    public int SaleId { get; private set; }
    public int LineNo { get; private set; }
    public int ProductId { get; private set; }
    public int PriceVersionId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Qty { get; private set; }
    public int FreeQty { get; private set; }
    public int? PromotionVersionId { get; private set; }
    public decimal PromoDiscount { get; private set; }
    public decimal LineNet { get; private set; }

    private SaleLine() { } // EF

    internal SaleLine(int lineNo, int productId, int priceVersionId, decimal unitPrice, int qty, int freeQty, int? promotionVersionId, decimal promoDiscount, decimal lineNet)
    {
        LineNo = lineNo;
        ProductId = productId;
        PriceVersionId = priceVersionId;
        UnitPrice = unitPrice;
        Qty = qty;
        FreeQty = freeQty;
        PromotionVersionId = promotionVersionId;
        PromoDiscount = promoDiscount;
        LineNet = lineNet;
    }
}

/// <summary>STM-talad-004</summary>
public enum SaleStatus
{
    Paid,
    Voided,
}
