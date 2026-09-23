using Talad.Application.Auth;
using Talad.Application.Sales;
using Talad.Domain.Catalog;

namespace Talad.Application.Catalog;

/// <summary>One change of price — ENT-002 · a history row of UI-talad-012.</summary>
public sealed record PriceChangeView(int Id, decimal PreviousPrice, decimal Price, string Source, int ChangedById, string ChangedByName, DateTimeOffset ChangedAt);

/// <summary>API-019 — UI-talad-012's summary and its price-history section, paged on its own (state "overflow").</summary>
public sealed record ProductDetail(
    int Id, string Name, string? Barcode, int StockQty, int LowStockThreshold, bool LowStock, decimal Price, bool HasImage,
    PagedResult<PriceChangeView> PriceHistory);

/// <summary>
/// UC-talad-017 · API-019 · API-022 — the owner changes a price from the stock screen or the sales screen's
/// shortcut, and reads its history. A change is a new ENT-002 row the product then points at; nothing is updated,
/// so a paid bill keeps the price it was paid at (AC-talad-060). The stock adjustments of API-019 are
/// FE-talad-023's and are not answered here yet. UC-talad-018 · API-023 — the owner discontinues a product.
/// </summary>
public sealed class ProductPricing(IProductRepository products, IUserAccountRepository accounts, TimeProvider clock)
{
    public const int PageSize = 20; // NFR-talad-006 — each section 20 rows a page, paged at the server

    /// <summary>API-019 — an ACTIVE product; unknown or discontinued is <see cref="ProductNotFoundException"/>.</summary>
    public async Task<ProductDetail> GetDetailAsync(int id, int historyPage, CancellationToken ct = default)
    {
        var product = await ActiveAsync(id, ct);
        historyPage = Math.Max(1, historyPage);
        var (items, total) = await products.PriceHistoryAsync(id, historyPage, PageSize, ct);
        var history = items.Select(h => new PriceChangeView(
            h.Version.Id, h.Version.PreviousPrice!.Value, h.Version.Price, h.Version.Source.Code(), h.Version.ChangedById, h.ChangedByName, h.Version.ChangedAt)).ToList();
        return new ProductDetail(
            product.Id, product.Name, product.Barcode, product.StockQty, product.LowStockThreshold, product.IsLowStock, product.CurrentPrice,
            product.ImagePath is not null, new PagedResult<PriceChangeView>(history, historyPage, PageSize, total));
    }

    /// <summary>API-022 — the new price, then the pointer (the row needs its id before the product can point at it).</summary>
    public async Task<ProductDetail> RepriceAsync(int id, decimal? price, string? source, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new PriceOwnerOnlyException();
        var product = await ActiveAsync(id, ct);
        var channel = PriceChangeSources.Parse(source) ?? throw new PriceInvalidException(PriceMessages.SourceRequired, "source");
        var version = product.Reprice(price, channel, owner, clock.GetUtcNow());
        products.Add(version);
        await products.SaveChangesAsync(ct);
        product.PointAtPrice(version);
        await products.SaveChangesAsync(ct);
        return await GetDetailAsync(id, 1, ct);
    }

    /// <summary>
    /// API-023 · UC-talad-018 — ACTIVE → DISCONTINUED. From then on the sales screen's search and the stock list
    /// leave it out and API-019 answers it as not found; its price versions are not touched, so the bills that
    /// sold it keep their price (AC-talad-085). Already discontinued or never there is not found.
    /// </summary>
    public async Task DiscontinueAsync(int id, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new ProductOwnerOnlyException();
        var product = await products.FindAsync(id, ct) ?? throw new ProductNotFoundException(id);
        product.Discontinue(owner);
        await products.SaveChangesAsync(ct);
    }

    private async Task<Product> ActiveAsync(int id, CancellationToken ct)
    {
        var product = await products.FindAsync(id, ct);
        return product is { IsActive: true } ? product : throw new ProductNotFoundException(id);
    }
}
