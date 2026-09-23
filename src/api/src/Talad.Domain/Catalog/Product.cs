namespace Talad.Domain.Catalog;

/// <summary>ENT-001 · สินค้า. Status follows STM-talad-001 (ACTIVE → DISCONTINUED, final).</summary>
public class Product
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    /// <summary>Optional; unique among ACTIVE products; searched in full at the sales screen.</summary>
    public string? Barcode { get; private set; }
    /// <summary>DEC-002 — the file lives on the server's disk; only its location is stored.</summary>
    public string? ImagePath { get; private set; }
    public int? CurrentPriceVersionId { get; private set; }
    public ProductPriceVersion? CurrentPriceVersion { get; private set; }
    /// <summary>BR-talad-007@v1 — never below zero; changed only by a sale, a cancellation or a stock adjustment.</summary>
    public int StockQty { get; private set; }
    public int LowStockThreshold { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Product() { } // EF

    public Product(string name, string? barcode, int stockQty, int lowStockThreshold, DateTimeOffset createdAt, string? imagePath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name must not be empty", nameof(name));
        if (stockQty < 0) throw new ArgumentOutOfRangeException(nameof(stockQty));
        if (lowStockThreshold < 0) throw new ArgumentOutOfRangeException(nameof(lowStockThreshold));
        Name = name.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        StockQty = stockQty;
        LowStockThreshold = lowStockThreshold;
        ImagePath = imagePath;
        Status = ProductStatus.Active;
        CreatedAt = createdAt;
    }

    public bool IsActive => Status == ProductStatus.Active;

    /// <summary>BR-talad-008@v1 — "ใกล้หมด" when what is left is at or below the owner's threshold.</summary>
    public bool IsLowStock => StockQty <= LowStockThreshold;

    public decimal CurrentPrice =>
        CurrentPriceVersion?.Price ?? throw new InvalidOperationException($"product {Id} has no current price version loaded");

    public void PointAtPrice(ProductPriceVersion version)
    {
        if (version.ProductId != Id) throw new ArgumentException("price version belongs to another product", nameof(version));
        CurrentPriceVersion = version;
        CurrentPriceVersionId = version.Id;
    }

    /// <summary>STM-talad-001 ACTIVE → DISCONTINUED (BR-talad-037@v1 — never a real delete).</summary>
    public void Discontinue() => Status = ProductStatus.Discontinued;
}

public enum ProductStatus
{
    Active,
    Discontinued,
}

/// <summary>ENT-002 · a price is never edited in place — every change is a new row (BR-talad-033@v1).</summary>
public class ProductPriceVersion
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public decimal Price { get; private set; }
    public decimal? PreviousPrice { get; private set; }
    public PriceChangeSource Source { get; private set; }
    public int ChangedById { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private ProductPriceVersion() { } // EF

    public ProductPriceVersion(int productId, decimal price, decimal? previousPrice, PriceChangeSource source, int changedById, DateTimeOffset changedAt)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price), "price must be greater than 0");
        if (decimal.Round(price, 2) != price) throw new ArgumentException("price has at most 2 decimals", nameof(price));
        ProductId = productId;
        Price = price;
        PreviousPrice = previousPrice;
        Source = source;
        ChangedById = changedById;
        ChangedAt = changedAt;
    }
}

public enum PriceChangeSource
{
    StockScreen,
    SalesScreen,
}
