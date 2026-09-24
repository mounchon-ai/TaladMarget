using Talad.Domain.Catalog;

namespace Talad.Application.Catalog;

public interface IProductRepository
{
    /// <summary>ACTIVE products only (BR-talad-037@v1), current price loaded, ordered by name.</summary>
    Task<(IReadOnlyList<Product> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct);

    /// <summary>The product with its current price, whatever its status.</summary>
    Task<Product?> FindAsync(int id, CancellationToken ct);

    /// <summary>BR-talad-033@v1 — the changes of one product's price (every version but the first), newest first, with who made each.</summary>
    Task<(IReadOnlyList<(ProductPriceVersion Version, string ChangedByName)> Items, int Total)> PriceHistoryAsync(int productId, int page, int pageSize, CancellationToken ct);

    /// <summary>ENT-003 — one product's adjustments, newest first, with who made each.</summary>
    Task<(IReadOnlyList<(StockAdjustment Adjustment, string AdjustedByName)> Items, int Total)> AdjustmentsAsync(int productId, int page, int pageSize, CancellationToken ct);

    /// <summary>BR-talad-041@v1 — whether any adjustment was already saved under this form's key.</summary>
    Task<bool> AdjustmentKeyTakenAsync(string requestKey, CancellationToken ct);

    void Add(ProductPriceVersion version);
    void Add(StockAdjustment adjustment);

    /// <summary>A second adjustment under a key already saved is <see cref="StockAdjustmentDuplicateException"/>.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record ProductCard(int Id, string Name, string? Barcode, decimal Price, int StockQty, bool LowStock, bool HasImage);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>API-003 · the cards on the sales screen — current price, what is left, and the low-stock flag (BR-talad-008@v1).</summary>
public sealed class ProductCatalog(IProductRepository products)
{
    public const int PageSize = 20; // API-003 · pageSize=20

    public async Task<PagedResult<ProductCard>> SearchAsync(string? term, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await products.SearchActiveAsync(term, page, PageSize, ct);
        return new PagedResult<ProductCard>(items.Select(ToCard).ToList(), page, PageSize, total);
    }

    public static ProductCard ToCard(Product p) =>
        new(p.Id, p.Name, p.Barcode, p.CurrentPrice, p.StockQty, p.IsLowStock, p.ImagePath is not null);
}
