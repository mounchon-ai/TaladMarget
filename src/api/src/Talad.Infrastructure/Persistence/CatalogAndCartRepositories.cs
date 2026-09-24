using Talad.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<(IReadOnlyList<(ProductPriceVersion Version, string ChangedByName)> Items, int Total)> PriceHistoryAsync(int productId, int page, int pageSize, CancellationToken ct)
    {
        // the first version has no previous price — it is the price the product was created with, not a change
        var changes = db.ProductPriceVersions.Where(v => v.ProductId == productId && v.PreviousPrice != null);
        var total = await changes.CountAsync(ct);
        var rows = await changes
            .OrderByDescending(v => v.ChangedAt).ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.UserAccounts, v => v.ChangedById, a => a.Id, (v, a) => new { Version = v, a.DisplayName })
            .ToListAsync(ct);
        return (rows.Select(r => (r.Version, r.DisplayName)).ToList(), total);
    }

    public async Task<(IReadOnlyList<(StockAdjustment Adjustment, string AdjustedByName)> Items, int Total)> AdjustmentsAsync(int productId, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.StockAdjustments.Where(a => a.ProductId == productId);
        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(a => a.AdjustedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.UserAccounts, a => a.AdjustedById, u => u.Id, (a, u) => new { Adjustment = a, u.DisplayName })
            .ToListAsync(ct);
        return (items.Select(r => (r.Adjustment, r.DisplayName)).ToList(), total);
    }

    public Task<bool> AdjustmentKeyTakenAsync(string requestKey, CancellationToken ct) =>
        db.StockAdjustments.AnyAsync(a => a.RequestKey == requestKey, ct);

    public void Add(ProductPriceVersion version) => db.ProductPriceVersions.Add(version);

    public void Add(StockAdjustment adjustment) => db.StockAdjustments.Add(adjustment);

    /// <summary>
    /// Two saves of the same form can both pass the key check before either commits (a double click, a resend
    /// racing the first); the unique index turns the second away, and it answers the same as the check would have.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: StockAdjustmentConfiguration.RequestKeyIndex,
        })
        {
            throw new StockAdjustmentDuplicateException();
        }
    }
}

internal sealed class CartRepository(TaladDbContext db) : ICartRepository
{
    public Task<Cart?> FindOpenAsync(int ownerId, CancellationToken ct) =>
        db.Carts
            .Include(c => c.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.CurrentPriceVersion)
            .Include(c => c.Member)
            .SingleOrDefaultAsync(c => c.OwnerId == ownerId && c.Status == CartStatus.Open, ct);

    public Task<Cart?> FindOwnAsync(int cartId, int ownerId, CancellationToken ct) =>
        db.Carts
            .Include(c => c.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.CurrentPriceVersion)
            .Include(c => c.Member)
            .SingleOrDefaultAsync(c => c.Id == cartId && c.OwnerId == ownerId, ct);

    public void Add(Cart cart) => db.Carts.Add(cart);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

internal sealed class SaleRepository(TaladDbContext db) : ISaleRepository
{
    public void Add(Sale sale) => db.Sales.Add(sale);

    /// <summary>
    /// Two presses of ชำระเงิน can both find the cart OPEN before either commits; the unique index on the cart turns the
    /// second away, and the retry then reads the cart PAID and answers "ตะกร้านี้ชำระเงินไปแล้ว" (BR-talad-039@v1).
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: SaleConfiguration.CartIndex,
        })
        {
            throw new ConcurrentUpdateException("cart");
        }
    }
}

/// <summary>
/// API-011 — a bill and what it points at, read without the ACTIVE filters the sales screen uses: a hidden member, a
/// discontinued product or promotion still names itself on the bill (BR-talad-040@v2 · BR-talad-036@v1).
/// </summary>
internal sealed class SaleReader(TaladDbContext db) : ISaleReader
{
    public async Task<SaleReading?> FindOwnAsync(int saleId, int sellerId, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking().Include(s => s.Lines).SingleOrDefaultAsync(s => s.Id == saleId && s.SellerId == sellerId, ct);
        if (sale is null) return null;

        var seller = await db.UserAccounts.AsNoTracking().Where(a => a.Id == sale.SellerId).Select(a => a.DisplayName).SingleAsync(ct);
        var member = sale.MemberId is { } m ? await db.Members.AsNoTracking().SingleAsync(x => x.Id == m, ct) : null;
        var productIds = sale.Lines.Select(l => l.ProductId).ToList();
        var names = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var versionIds = sale.Lines.Where(l => l.PromotionVersionId is not null).Select(l => l.PromotionVersionId!.Value).ToList();
        if (sale.BillPromotionVersionId is { } bill) versionIds.Add(bill);
        var promotions = await db.PromotionVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => new PromotionRef(v.PromotionId, v.Name), ct);
        var rate = sale.MemberDiscountVersionId is { } d
            ? await db.MemberDiscountVersions.AsNoTracking().Where(v => v.Id == d).Select(v => v.RatePercent).SingleAsync(ct)
            : 0;
        return new SaleReading(sale, seller, member, names, promotions, rate);
    }
}

/// <summary>The request's DbContext — forgetting what it tracked makes the next read come from the database.</summary>
internal sealed class UnitOfWork(TaladDbContext db) : IUnitOfWork
{
    public void Reset() => db.ChangeTracker.Clear();
}
