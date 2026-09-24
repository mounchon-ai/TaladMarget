using Talad.Application.Catalog;
using Talad.Application.Members;
using Talad.Domain.Catalog;
using Talad.Domain.Sales;

namespace Talad.Application.Sales;

public interface ICartRepository
{
    /// <summary>The OPEN cart this person opened, lines and their products' current prices loaded.</summary>
    Task<Cart?> FindOpenAsync(int ownerId, CancellationToken ct);

    /// <summary>The cart <paramref name="cartId"/> if <paramref name="ownerId"/> opened it, OPEN or PAID, loaded as <see cref="FindOpenAsync"/> loads it.</summary>
    Task<Cart?> FindOwnAsync(int cartId, int ownerId, CancellationToken ct);
    void Add(Cart cart);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record CartLineView(int ProductId, string Name, decimal Price, int Qty, decimal LineTotal, int StockQty, bool LowStock);

/// <summary>
/// `Subtotal` is price × qty before any promotion or member discount — those are UC-talad-004's.
/// `Member` is who the cart is bound to, or null (API-004 · API-008).
/// </summary>
public sealed record CartView(int Id, string Status, IReadOnlyList<CartLineView> Lines, decimal Subtotal, MemberView? Member);

public sealed class ProductNotFoundException(int productId) : Exception($"product {productId} does not exist or is no longer sold");

/// <summary>
/// UC-talad-001 · API-004..007. Every call acts on the caller's own OPEN cart — there is no parameter that
/// names someone else's (BR-talad-020@v1).
/// </summary>
public sealed class CartService(ICartRepository carts, IProductRepository products, IMemberRepository members, TimeProvider clock)
{
    /// <summary>API-004 — the caller's OPEN cart, opened now if they have none.</summary>
    public async Task<CartView> GetAsync(int ownerId, CancellationToken ct = default) =>
        View(await OpenCartAsync(ownerId, ct));

    /// <summary>API-005</summary>
    public async Task<CartView> AddOneAsync(int ownerId, int productId, CancellationToken ct = default)
    {
        var cart = await OpenCartAsync(ownerId, ct);
        var product = await SellableAsync(productId, ct);
        cart.AddOne(product, clock.GetUtcNow());
        await carts.SaveChangesAsync(ct);
        return View(cart);
    }

    /// <summary>API-006</summary>
    public async Task<CartView> SetQtyAsync(int ownerId, int productId, int qty, CancellationToken ct = default)
    {
        var cart = await OpenCartAsync(ownerId, ct);
        var product = await products.FindAsync(productId, ct) ?? throw new ProductNotFoundException(productId);
        cart.SetQty(product, qty);
        await carts.SaveChangesAsync(ct);
        return View(cart);
    }

    /// <summary>API-007</summary>
    public async Task<CartView> RemoveAsync(int ownerId, int productId, CancellationToken ct = default)
    {
        var cart = await OpenCartAsync(ownerId, ct);
        cart.Remove(productId);
        await carts.SaveChangesAsync(ct);
        return View(cart);
    }

    /// <summary>
    /// API-008 — bind an ACTIVE member to the caller's own cart, or unbind with null (BR-talad-004@v1 ·
    /// BR-talad-020@v1). A member who does not exist or is hidden is not found (BR-talad-040@v2).
    /// </summary>
    public async Task<CartView> SetMemberAsync(int ownerId, int? memberId, CancellationToken ct = default)
    {
        var cart = await OpenCartAsync(ownerId, ct);
        if (memberId is { } id)
        {
            var member = await members.FindAsync(id, ct) ?? throw new MemberNotBindableException(id);
            cart.AttachMember(member);
        }
        else
        {
            cart.DetachMember();
        }
        await carts.SaveChangesAsync(ct);
        return View(cart);
    }

    private async Task<Cart> OpenCartAsync(int ownerId, CancellationToken ct)
    {
        var cart = await carts.FindOpenAsync(ownerId, ct);
        if (cart is not null) return cart;
        cart = new Cart(ownerId, clock.GetUtcNow());
        carts.Add(cart);
        await carts.SaveChangesAsync(ct);
        return cart;
    }

    /// <summary>A product that is gone or discontinued is not found at all (BR-talad-037@v1).</summary>
    private async Task<Product> SellableAsync(int productId, CancellationToken ct)
    {
        var product = await products.FindAsync(productId, ct);
        return product is { IsActive: true } ? product : throw new ProductNotFoundException(productId);
    }

    private static CartView View(Cart cart)
    {
        var lines = cart.Lines
            .OrderBy(l => l.AddedAt)
            .Select(l => new CartLineView(l.ProductId, l.Product.Name, l.Product.CurrentPrice, l.Qty, l.Product.CurrentPrice * l.Qty, l.Product.StockQty, l.Product.IsLowStock))
            .ToList();
        return new CartView(cart.Id, cart.Status.ToString(), lines, lines.Sum(l => l.LineTotal),
            cart.Member is { } m ? MemberView.Of(m) : null);
    }
}
