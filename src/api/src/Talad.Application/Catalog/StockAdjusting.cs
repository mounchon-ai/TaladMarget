using Talad.Application.Auth;
using Talad.Application.Sales;
using Talad.Domain.Catalog;

namespace Talad.Application.Catalog;

/// <summary>
/// UC-talad-020 · API-024 — the owner records one hand change of a product's stock (BR-talad-032@v1). The product's
/// new stock and the ENT-003 row are saved together; the row is never edited afterwards. The form's key is asked
/// about first: a resend of a form already saved answers "บันทึกไปแล้ว" whatever the stock is now (BR-talad-041@v1).
/// </summary>
public sealed class StockAdjusting(IProductRepository products, IUserAccountRepository accounts, ProductPricing pricing, TimeProvider clock)
{
    /// <summary>
    /// API-024 — <paramref name="quantity"/> and <paramref name="countedQty"/> come as typed; one that is not a whole
    /// number is refused under its own field, the same as one missing.
    /// </summary>
    public async Task<ProductDetail> AdjustAsync(
        int id, string? reason, decimal? quantity, decimal? countedQty, string? note, string? requestKey, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new StockOwnerOnlyException();
        var product = await products.FindAsync(id, ct);
        if (product is not { IsActive: true }) throw new ProductNotFoundException(id);
        if (string.IsNullOrWhiteSpace(requestKey)) throw new StockAdjustmentInvalidException(StockAdjustmentMessages.RequestKeyRequired, "requestKey");
        var key = requestKey.Trim();
        if (await products.AdjustmentKeyTakenAsync(key, ct)) throw new StockAdjustmentDuplicateException();

        var adjustment = product.Adjust(
            StockAdjustmentReasons.Parse(reason), WholeNumber(quantity), WholeNumber(countedQty), note, key, owner, clock.GetUtcNow());
        products.Add(adjustment);
        await products.SaveChangesAsync(ct);
        return await pricing.GetDetailAsync(id, 1, 1, ct);
    }

    private static int? WholeNumber(decimal? value) =>
        value is { } v && decimal.Truncate(v) == v && v >= int.MinValue && v <= int.MaxValue ? (int)v : null;
}
