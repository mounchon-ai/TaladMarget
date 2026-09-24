namespace Talad.Domain.Catalog;

/// <summary>
/// ENT-003 · รายการปรับสต็อก — one hand change of a product's stock, with who, when and why (BR-talad-032@v1).
/// Written once: a later adjustment is a new row, never an edit of this one. <see cref="RequestKey"/> is the key the
/// form minted when it opened; a second save with the same key is refused (BR-talad-041@v1).
/// </summary>
public class StockAdjustment
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public StockAdjustmentReason Reason { get; private set; }
    /// <summary>Never 0. For a recount it is what was counted minus what the system held.</summary>
    public int QuantityDelta { get; private set; }
    /// <summary>What was counted on the shelf — a recount's alone, null otherwise.</summary>
    public int? CountedQty { get; private set; }
    public string? Note { get; private set; }
    public string RequestKey { get; private set; } = null!;
    public int AdjustedById { get; private set; }
    public DateTimeOffset AdjustedAt { get; private set; }

    private StockAdjustment() { } // EF

    internal StockAdjustment(int productId, StockAdjustmentReason reason, int quantityDelta, int? countedQty, string? note, string requestKey, int adjustedById, DateTimeOffset adjustedAt)
    {
        ProductId = productId;
        Reason = reason;
        QuantityDelta = quantityDelta;
        CountedQty = countedQty;
        Note = note;
        RequestKey = requestKey;
        AdjustedById = adjustedById;
        AdjustedAt = adjustedAt;
    }
}

/// <summary>ENT-003.reason — chosen from the list only (BR-talad-032@v1).</summary>
public enum StockAdjustmentReason
{
    Receive,
    Spoiled,
    Recount,
}

/// <summary>ENT-003.reason — the codes design gave the three reasons.</summary>
public static class StockAdjustmentReasons
{
    public static string Code(this StockAdjustmentReason reason) => reason switch
    {
        StockAdjustmentReason.Receive => "RECEIVE",
        StockAdjustmentReason.Spoiled => "SPOILED",
        _ => "RECOUNT",
    };

    public static StockAdjustmentReason? Parse(string? code) => code?.Trim() switch
    {
        "RECEIVE" => StockAdjustmentReason.Receive,
        "SPOILED" => StockAdjustmentReason.Spoiled,
        "RECOUNT" => StockAdjustmentReason.Recount,
        _ => null,
    };
}

/// <summary>
/// The sentences a person reads. <see cref="NegativeStock"/> and <see cref="Duplicate"/> are BR-talad-032@v1's and
/// BR-talad-041@v1's word for word; design words none of the others, so those are dev's (FE-talad-023).
/// </summary>
public static class StockAdjustmentMessages
{
    public static string NegativeStock(int left) => $"จำนวนคงเหลือต้องไม่ติดลบ (เหลือ {left})";
    public const string Duplicate = "รายการปรับสต็อกนี้บันทึกไปแล้ว";
    public const string ReasonRequired = "กรุณาเลือกเหตุผล";
    public const string QuantityRequired = "กรุณากรอกจำนวนเต็มที่ไม่ใช่ 0";
    public const string CountedRequired = "กรุณากรอกจำนวนที่นับได้เป็นจำนวนเต็ม 0 ขึ้นไป";
    public const string NoDifference = "จำนวนที่นับได้เท่ากับคงเหลือในระบบ ไม่มีส่วนต่างให้บันทึก";
    public const string TooMany = "จำนวนมากเกินกว่าที่ระบบรับได้";
    public const string RequestKeyRequired = "ฟอร์มนี้ไม่มีคีย์กันบันทึกซ้ำ กรุณาเปิดฟอร์มใหม่";
}

/// <summary>An adjustment refused — the message sits under the field it names.</summary>
public sealed class StockAdjustmentInvalidException(string message, string field) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>BR-talad-041@v1 — this form's key was saved already; nothing is adjusted a second time.</summary>
public sealed class StockAdjustmentDuplicateException() : Exception(StockAdjustmentMessages.Duplicate);

/// <summary>ACL-022 · ENT-003.adjustedBy — only the owner adjusts stock.</summary>
public sealed class StockOwnerOnlyException() : Exception("only the owner may adjust stock");
