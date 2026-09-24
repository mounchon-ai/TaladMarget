using Talad.Domain.Accounts;

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

    /// <summary>
    /// API-022 · UC-talad-017 — a new price in force from now, from the stock screen or the sales screen's shortcut
    /// (BR-talad-019@v1): the owner's alone, on a product still sold (ACL-019). It is a new ENT-002 row carrying
    /// the price it replaces; the row in force stays as it was for the bills that used it (BR-talad-033@v1).
    /// The caller saves the row, then points the product at it.
    /// </summary>
    public ProductPriceVersion Reprice(decimal? newPrice, PriceChangeSource source, UserAccount by, DateTimeOffset at)
    {
        if (by.Role != UserRole.Owner) throw new PriceOwnerOnlyException();
        if (!IsActive) throw new ProductNotActiveException(Id);
        var current = CurrentPriceVersion ?? throw new InvalidOperationException($"product {Id} has no current price version loaded");
        if (newPrice is not { } price || price <= 0 || decimal.Round(price, 2) != price) throw new PriceInvalidException(PriceMessages.Invalid);
        if (price > ProductPriceVersion.MaxPrice) throw new PriceInvalidException(PriceMessages.TooHigh);
        return new ProductPriceVersion(Id, price, current.Price, source, by.Id, at);
    }

    /// <summary>
    /// UC-talad-003 · BR-talad-007@v1 — the pieces a paid bill takes, free ones included. Whether the product is still
    /// sold and enough is left is asked by the sale first, in the words the person reads; this only refuses to go
    /// below zero, as the database does.
    /// </summary>
    public void RemoveSold(int qty)
    {
        if (qty < 1 || qty > StockQty) throw new ArgumentOutOfRangeException(nameof(qty), $"product {Id} has {StockQty} left, {qty} asked");
        StockQty -= qty;
    }

    /// <summary>
    /// API-024 · UC-talad-020 — the owner's hand change of what is left (ACL-022), on a product still sold. RECEIVE and
    /// SPOILED carry the change itself, + or − (BR-talad-032@v1 does not tie the sign to the reason); RECOUNT carries
    /// what was counted and the change is worked out from what the system holds. The change is never 0 and never
    /// leaves the stock below zero. The caller saves the product and the row it returns together.
    /// </summary>
    public StockAdjustment Adjust(
        StockAdjustmentReason? reason, int? quantity, int? countedQty, string? note, string requestKey, UserAccount by, DateTimeOffset at)
    {
        if (by.Role != UserRole.Owner) throw new StockOwnerOnlyException();
        if (!IsActive) throw new ProductNotActiveException(Id);
        if (reason is not { } why) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.ReasonRequired, "reason");

        long delta;
        int? counted = null;
        if (why == StockAdjustmentReason.Recount)
        {
            if (countedQty is not { } c || c < 0) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.CountedRequired, "countedQty");
            delta = (long)c - StockQty;
            if (delta == 0) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.NoDifference, "countedQty");
            counted = c;
        }
        else
        {
            if (quantity is not { } q || q == 0) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.QuantityRequired, "quantity");
            if (StockQty + (long)q < 0) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.NegativeStock(StockQty), "quantity");
            if (StockQty + (long)q > int.MaxValue) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.TooMany, "quantity");
            delta = q;
        }

        StockQty += (int)delta;
        return new StockAdjustment(Id, why, (int)delta, counted, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), requestKey, by.Id, at);
    }

    /// <summary>
    /// API-023 · UC-talad-018 · STM-talad-001 ACTIVE → DISCONTINUED, final: the owner's alone, on a product still
    /// sold (ACL-020). Whether or not a bill ever sold it, a product is never deleted (BR-talad-037@v1) — its price
    /// versions stay for the bills that point at them, and an open cart that holds it keeps the line until taken out.
    /// </summary>
    public void Discontinue(UserAccount by)
    {
        if (by.Role != UserRole.Owner) throw new ProductOwnerOnlyException();
        if (!IsActive) throw new ProductNotActiveException(Id);
        Status = ProductStatus.Discontinued;
    }
}

public enum ProductStatus
{
    Active,
    Discontinued,
}

/// <summary>ENT-002 · a price is never edited in place — every change is a new row (BR-talad-033@v1).</summary>
public class ProductPriceVersion
{
    /// <summary>ENT-002.price is numeric(12,2).</summary>
    public const decimal MaxPrice = 9_999_999_999.99m;

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

/// <summary>ENT-002.source — the codes design gave the two ways a price is changed.</summary>
public static class PriceChangeSources
{
    public static string Code(this PriceChangeSource source) => source == PriceChangeSource.SalesScreen ? "SALES_SCREEN" : "STOCK_SCREEN";

    public static PriceChangeSource? Parse(string? code) => code?.Trim() switch
    {
        "STOCK_SCREEN" => PriceChangeSource.StockScreen,
        "SALES_SCREEN" => PriceChangeSource.SalesScreen,
        _ => null,
    };
}

/// <summary>
/// UI-talad-013 state "error" says a price not above 0 is refused under its field but gives no wording, so these
/// sentences are dev's (FE-talad-019).
/// </summary>
public static class PriceMessages
{
    public const string Invalid = "ราคาต้องมากกว่า 0 และมีทศนิยมไม่เกิน 2 ตำแหน่ง";
    public const string TooHigh = "ราคาสูงเกินกว่าที่ระบบรับได้";
    public const string SourceRequired = "กรุณาระบุช่องทางที่แก้ราคา";
}

/// <summary>A price refused — the message sits under the field it names.</summary>
public sealed class PriceInvalidException(string message, string field = "price") : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>ACL-019 · ENT-002.changedBy — only the owner changes a price.</summary>
public sealed class PriceOwnerOnlyException() : Exception("only the owner may change a price");

/// <summary>ACL-020 — only the owner discontinues a product.</summary>
public sealed class ProductOwnerOnlyException() : Exception("only the owner may discontinue a product");

/// <summary>STM-talad-001 — a discontinued product's price is not changed, and it is not discontinued again.</summary>
public sealed class ProductNotActiveException(int productId) : Exception($"product {productId} is discontinued");
