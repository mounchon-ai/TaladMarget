using Talad.Domain.Catalog;
using Talad.Domain.Members;

namespace Talad.Domain.Sales;

/// <summary>
/// ENT-009 · ตะกร้า. Belongs to the person who opened it and nobody else (BR-talad-020@v1); one OPEN
/// cart per person; lines change only while OPEN (BR-talad-001@v1 · STM-talad-005).
/// An open cart neither takes nor reserves stock — it only refuses to hold more than is left (BR-talad-007@v1).
/// </summary>
public class Cart
{
    private readonly List<CartLine> _lines = [];

    public int Id { get; private set; }
    public int OwnerId { get; private set; }
    public CartStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public IReadOnlyList<CartLine> Lines => _lines;

    /// <summary>ENT-009.member — the member this sale is for, if any (BR-talad-004@v1).</summary>
    public int? MemberId { get; private set; }
    public Member? Member { get; private set; }

    private Cart() { } // EF

    public Cart(int ownerId, DateTimeOffset openedAt)
    {
        OwnerId = ownerId;
        Status = CartStatus.Open;
        OpenedAt = openedAt;
    }

    /// <summary>API-005 — one more of this product: a new line at 1, or the existing line + 1.</summary>
    public CartLine AddOne(Product product, DateTimeOffset now)
    {
        EnsureOpen();
        EnsureSellable(product);
        var line = _lines.SingleOrDefault(l => l.ProductId == product.Id);
        var wanted = (line?.Qty ?? 0) + 1;
        EnsureInStock(product, wanted);
        if (line is null)
        {
            line = new CartLine(product, 1, now);
            _lines.Add(line);
        }
        else
        {
            line.SetQty(wanted);
        }
        return line;
    }

    /// <summary>API-006 — set the line's quantity; 0 takes the line out (ENT-010.qty).</summary>
    public void SetQty(Product product, int qty)
    {
        EnsureOpen();
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        var line = _lines.SingleOrDefault(l => l.ProductId == product.Id) ?? throw new CartLineNotFoundException(product.Id);
        if (qty == 0)
        {
            _lines.Remove(line);
            return;
        }
        EnsureSellable(product);
        EnsureInStock(product, qty);
        line.SetQty(qty);
    }

    /// <summary>API-007 — take the line out.</summary>
    public void Remove(int productId)
    {
        EnsureOpen();
        var line = _lines.SingleOrDefault(l => l.ProductId == productId) ?? throw new CartLineNotFoundException(productId);
        _lines.Remove(line);
    }

    /// <summary>
    /// API-008 — bind a member to this cart, replacing any bound before. Only an ACTIVE member may be
    /// bound; a hidden one is as if they were not there (BR-talad-040@v2 · ENT-009.member).
    /// </summary>
    public void AttachMember(Member member)
    {
        EnsureOpen();
        if (member.Status != MemberStatus.Active) throw new MemberNotBindableException(member.Id);
        Member = member;
        MemberId = member.Id;
    }

    /// <summary>API-008 with no member — the cart is for nobody in particular again.</summary>
    public void DetachMember()
    {
        EnsureOpen();
        Member = null;
        MemberId = null;
    }

    /// <summary>
    /// UC-talad-003 — can this cart be paid now, asked in the order a person reads the answers: already paid
    /// (BR-talad-039@v1) · nothing in it · a line no longer sold (BR-talad-037@v1) · a line with less left than it holds
    /// (BR-talad-007@v1) · a bound member no longer ACTIVE (ENT-009.member). The first that fails is the answer.
    /// </summary>
    public void EnsurePayable()
    {
        EnsureOpen();
        if (_lines.Count == 0) throw new CartEmptyException();
        foreach (var line in _lines.OrderBy(l => l.AddedAt))
        {
            EnsureSellable(line.Product);
            EnsureInStock(line.Product, line.Qty);
        }
        if (Member is { } m && m.Status != MemberStatus.Active) throw new MemberNotActiveAtCheckoutException(m.Name);
    }

    /// <summary>
    /// STM-talad-005 OPEN → PAID, in the same transaction as the bill; from then on nothing in it changes
    /// (BR-talad-001@v1). The bill asks <see cref="EnsurePayable"/> before it takes the stock, so only OPEN is asked here.
    /// </summary>
    internal void MarkPaid()
    {
        EnsureOpen();
        Status = CartStatus.Paid;
    }

    private void EnsureOpen()
    {
        if (Status != CartStatus.Open) throw new CartNotOpenException();
    }

    private static void EnsureSellable(Product product)
    {
        if (!product.IsActive) throw new ProductDiscontinuedException(product.Name);
    }

    private static void EnsureInStock(Product product, int wanted)
    {
        if (wanted > product.StockQty) throw new InsufficientStockException(product.Name, product.StockQty);
    }
}

/// <summary>ENT-010 · one line per product per cart, free gifts included.</summary>
public class CartLine
{
    public int CartId { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int Qty { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    private CartLine() { } // EF

    internal CartLine(Product product, int qty, DateTimeOffset addedAt)
    {
        Product = product;
        ProductId = product.Id;
        Qty = qty;
        AddedAt = addedAt;
    }

    internal void SetQty(int qty) => Qty = qty;
}

/// <summary>STM-talad-005</summary>
public enum CartStatus
{
    Open,
    Paid,
}

public abstract class CartRuleException(string message) : Exception(message);

/// <summary>BR-talad-007@v1 — the sentence the person reads, word for word.</summary>
public sealed class InsufficientStockException(string productName, int left)
    : CartRuleException($"{productName} คงเหลือไม่พอ (เหลือ {left})");

/// <summary>BR-talad-037@v1</summary>
public sealed class ProductDiscontinuedException(string productName)
    : CartRuleException($"{productName} เลิกขายแล้ว กรุณาเอาออกจากตะกร้า");

/// <summary>BR-talad-001@v1 — a paid cart is not edited.</summary>
public sealed class CartNotOpenException() : CartRuleException("ตะกร้านี้ชำระเงินไปแล้ว");

/// <summary>UI-talad-002 state "empty" — the sentence the empty cart shows, also the answer to paying it.</summary>
public sealed class CartEmptyException() : CartRuleException("ยังไม่มีสินค้าในตะกร้า");

/// <summary>ENT-009.member must be ACTIVE when paid — design words no sentence for it, so this one is dev's (FE-talad-033).</summary>
public sealed class MemberNotActiveAtCheckoutException(string memberName)
    : CartRuleException($"สมาชิก {memberName} ถูกลบแล้ว กรุณาเอาสมาชิกออกจากตะกร้าก่อนชำระเงิน");

public sealed class CartLineNotFoundException(int productId) : CartRuleException($"product {productId} is not in the cart");

/// <summary>BR-talad-040@v2 — the member does not exist, or is hidden; either way there is nobody to bind.</summary>
public sealed class MemberNotBindableException(int memberId) : CartRuleException($"member {memberId} does not exist or is hidden");
