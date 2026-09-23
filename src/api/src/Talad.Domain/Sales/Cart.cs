using Talad.Domain.Catalog;

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

public sealed class CartLineNotFoundException(int productId) : CartRuleException($"product {productId} is not in the cart");
