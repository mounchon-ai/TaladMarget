using Microsoft.EntityFrameworkCore;
using Talad.Application.Catalog;
using Talad.Application.Sales;
using Talad.Domain.Catalog;
using Talad.Domain.Sales;

namespace Talad.Infrastructure.Persistence;

internal sealed class ProductRepository(TaladDbContext db) : IProductRepository
{
    public async Task<(IReadOnlyList<Product> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.Where(p => p.Status == ProductStatus.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            // UC-talad-001 — part of the name, or the whole barcode
            var t = term.Trim();
            var lowered = t.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(lowered) || p.Barcode == t);
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(p => p.CurrentPriceVersion)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Product?> FindAsync(int id, CancellationToken ct) =>
        db.Products.Include(p => p.CurrentPriceVersion).SingleOrDefaultAsync(p => p.Id == id, ct);
}

internal sealed class CartRepository(TaladDbContext db) : ICartRepository
{
    public Task<Cart?> FindOpenAsync(int ownerId, CancellationToken ct) =>
        db.Carts
            .Include(c => c.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.CurrentPriceVersion)
            .Include(c => c.Member)
            .SingleOrDefaultAsync(c => c.OwnerId == ownerId && c.Status == CartStatus.Open, ct);

    public void Add(Cart cart) => db.Carts.Add(cart);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
