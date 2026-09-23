using System.Globalization;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;

namespace Talad.Domain.Promotions;

/// <summary>
/// ENT-005 · โปรโมชั่น. Status follows STM-talad-002 (ACTIVE → DISCONTINUED, final). Its conditions live in
/// versions: every create or edit adds a row and the promotion points at the newest, so a bill keeps the
/// version it was paid under (BR-talad-035@v1 · BR-talad-036@v1 — insert-only).
/// Only the owner creates or edits one (ACL-023). Whether today falls inside its dates is asked at checkout
/// (BR-talad-015@v1), not here — a promotion past its end date is still ACTIVE.
/// </summary>
public class Promotion
{
    public int Id { get; private set; }
    public int? CurrentVersionId { get; private set; }
    public PromotionVersion? CurrentVersion { get; private set; }
    public PromotionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Promotion() { } // EF

    private Promotion(DateTimeOffset createdAt)
    {
        Status = PromotionStatus.Active; // STM-talad-002 initial
        CreatedAt = createdAt;
    }

    /// <summary>API-027 — a new promotion; its first version is drafted once it has an id.</summary>
    public static Promotion Open(UserAccount by, DateTimeOffset at)
    {
        EnsureOwner(by);
        return new Promotion(at);
    }

    /// <summary>API-027 · API-028 — a new version of this promotion's conditions, not yet in force.</summary>
    public PromotionVersion Draft(PromotionConditions conditions, UserAccount by, DateTimeOffset at)
    {
        EnsureOwner(by);
        if (Status != PromotionStatus.Active) throw new PromotionNotActiveException(Id);
        return new PromotionVersion(Id, conditions, by.Id, at);
    }

    /// <summary>The version in force from now on; the one before stays for the bills that point at it.</summary>
    public void PointAt(PromotionVersion version)
    {
        if (version.PromotionId != Id) throw new ArgumentException("version belongs to another promotion", nameof(version));
        CurrentVersion = version;
        CurrentVersionId = version.Id;
    }

    /// <summary>
    /// API-029 · STM-talad-002 ACTIVE → DISCONTINUED, final (ACL-024). Whether or not a bill ever used it,
    /// a promotion is never deleted (BR-talad-037@v1): every version stays for the bills that point at it.
    /// </summary>
    public void Discontinue(UserAccount by)
    {
        EnsureOwner(by);
        if (Status != PromotionStatus.Active) throw new PromotionNotActiveException(Id);
        Status = PromotionStatus.Discontinued;
    }

    private static void EnsureOwner(UserAccount by)
    {
        if (by.Role != UserRole.Owner) throw new PromotionOwnerOnlyException();
    }
}

/// <summary>STM-talad-002</summary>
public enum PromotionStatus
{
    Active,
    Discontinued,
}

/// <summary>ENT-006.type — the six forms, by the codes design gave them.</summary>
public enum PromotionType
{
    ItemPercent,
    BillPercent,
    BuyXGetY,
    BuyAbGetY,
    BuyAbPercent,
    BuyXPercent,
}

public static class PromotionTypes
{
    private static readonly Dictionary<PromotionType, string> Codes = new()
    {
        [PromotionType.ItemPercent] = "ITEM_PERCENT",
        [PromotionType.BillPercent] = "BILL_PERCENT",
        [PromotionType.BuyXGetY] = "BUY_X_GET_Y",
        [PromotionType.BuyAbGetY] = "BUY_AB_GET_Y",
        [PromotionType.BuyAbPercent] = "BUY_AB_PERCENT",
        [PromotionType.BuyXPercent] = "BUY_X_PERCENT",
    };

    public static string Code(this PromotionType type) => Codes[type];

    public static PromotionType? Parse(string? code)
    {
        var wanted = code?.Trim();
        foreach (var (type, value) in Codes)
            if (value == wanted) return type;
        return null;
    }

    /// <summary>ENT-006 — which fields each form must have; every other field is cleared.</summary>
    public static bool UsesProductA(this PromotionType t) => t != PromotionType.BillPercent;
    public static bool UsesQtyA(this PromotionType t) => t is PromotionType.BuyXGetY or PromotionType.BuyAbGetY or PromotionType.BuyAbPercent or PromotionType.BuyXPercent;
    public static bool UsesProductB(this PromotionType t) => t is PromotionType.BuyAbGetY or PromotionType.BuyAbPercent;
    public static bool UsesFree(this PromotionType t) => t is PromotionType.BuyXGetY or PromotionType.BuyAbGetY;
    public static bool UsesRate(this PromotionType t) => t is PromotionType.ItemPercent or PromotionType.BillPercent or PromotionType.BuyAbPercent or PromotionType.BuyXPercent;
    public static bool UsesMinSubtotal(this PromotionType t) => t == PromotionType.BillPercent;
}

/// <summary>What the owner typed — every value loose, so a wrong one is answered under its field, not by the JSON reader.</summary>
public sealed record PromotionTerms(
    string? Name, string? Type,
    decimal? ProductA, decimal? QtyA, decimal? ProductB, decimal? QtyB, decimal? FreeProduct, decimal? FreeQty,
    decimal? RatePercent, decimal? MinSubtotal, string? StartDate, string? EndDate);

/// <summary>ENT-006 conditions that passed every rule the domain can check alone (BR-talad-009 · 011..015).</summary>
public sealed record PromotionConditions(
    string Name, PromotionType Type,
    int? ProductAId, int? QtyA, int? ProductBId, int? QtyB, int? FreeProductId, int? FreeQty,
    int? RatePercent, decimal? MinSubtotal, DateOnly StartDate, DateOnly? EndDate)
{
    /// <summary>
    /// Every field that is wrong, keyed by its ENT-006 attribute name. Fields the chosen form does not use
    /// are cleared, so nothing downstream reads a stray value. Whether a product exists and is still sold
    /// needs the catalog, so the caller asks that after this.
    /// </summary>
    public static PromotionConditions Check(PromotionTerms t)
    {
        var errors = new List<PromotionFieldError>();
        void Fail(string field, string message) => errors.Add(new(field, message));

        var name = (t.Name ?? "").Trim();
        if (name.Length == 0) Fail(PromotionFields.Name, PromotionMessages.Required);

        var type = PromotionTypes.Parse(t.Type);
        if (type is null) Fail(PromotionFields.Type, PromotionMessages.TypeRequired);

        int? Id(decimal? v, string field, bool used)
        {
            if (!used) return null;
            if (v is null) { Fail(field, PromotionMessages.Required); return null; }
            if (v <= 0 || v != decimal.Truncate(v.Value) || v > int.MaxValue) { Fail(field, PromotionMessages.ProductNotSellable); return null; }
            return (int)v;
        }

        int? Qty(decimal? v, string field, bool used)
        {
            if (!used) return null;
            if (v is null) { Fail(field, PromotionMessages.Required); return null; }
            if (v < 1 || v != decimal.Truncate(v.Value) || v > int.MaxValue) { Fail(field, PromotionMessages.QtyAtLeastOne); return null; }
            return (int)v;
        }

        var t2 = type ?? PromotionType.BillPercent; // only read when type parsed; errors already carry a bad type
        var parsed = type is not null;
        var productA = Id(t.ProductA, PromotionFields.ProductA, parsed && t2.UsesProductA());
        var qtyA = Qty(t.QtyA, PromotionFields.QtyA, parsed && t2.UsesQtyA());
        var productB = Id(t.ProductB, PromotionFields.ProductB, parsed && t2.UsesProductB());
        var qtyB = Qty(t.QtyB, PromotionFields.QtyB, parsed && t2.UsesProductB());
        var free = Id(t.FreeProduct, PromotionFields.FreeProduct, parsed && t2.UsesFree());
        var freeQty = Qty(t.FreeQty, PromotionFields.FreeQty, parsed && t2.UsesFree());

        int? rate = null;
        if (parsed && t2.UsesRate())
        {
            if (t.RatePercent is not { } r) Fail(PromotionFields.RatePercent, PromotionMessages.Required);
            else if (r < 0 || r > 100 || r != decimal.Truncate(r)) Fail(PromotionFields.RatePercent, PromotionMessages.RateOutOfRange);
            else rate = (int)r;
        }

        decimal? minSubtotal = null;
        if (parsed && t2.UsesMinSubtotal())
        {
            if (t.MinSubtotal is not { } m) Fail(PromotionFields.MinSubtotal, PromotionMessages.Required);
            else if (m < 0 || m != decimal.Round(m, 2) || m >= 10_000_000_000m) Fail(PromotionFields.MinSubtotal, PromotionMessages.MinSubtotalInvalid);
            else minSubtotal = m;
        }

        // BR-talad-015@v1 — a start date always; an end date may be empty, and is never before the start
        var start = ParseDate(t.StartDate);
        if (string.IsNullOrWhiteSpace(t.StartDate)) Fail(PromotionFields.StartDate, PromotionMessages.StartRequired);
        else if (start is null) Fail(PromotionFields.StartDate, PromotionMessages.DateInvalid);
        var end = ParseDate(t.EndDate);
        if (!string.IsNullOrWhiteSpace(t.EndDate) && end is null) Fail(PromotionFields.EndDate, PromotionMessages.DateInvalid);
        else if (start is not null && end is not null && end < start) Fail(PromotionFields.EndDate, PromotionMessages.EndBeforeStart);

        if (errors.Count > 0) throw new PromotionInvalidException(errors);
        return new PromotionConditions(name, type!.Value, productA, qtyA, productB, qtyB, free, freeQty, rate, minSubtotal, start!.Value, end);
    }

    private static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>The product ids this version names, for the catalog check.</summary>
    public IEnumerable<(string Field, int Id)> Products()
    {
        if (ProductAId is { } a) yield return (PromotionFields.ProductA, a);
        if (ProductBId is { } b) yield return (PromotionFields.ProductB, b);
        if (FreeProductId is { } f) yield return (PromotionFields.FreeProduct, f);
    }
}

/// <summary>ENT-006 · เวอร์ชันเงื่อนไขโปรโมชั่น — written once, never changed (BR-talad-036@v1).</summary>
public class PromotionVersion
{
    public int Id { get; private set; }
    public int PromotionId { get; private set; }
    public string Name { get; private set; } = null!;
    public PromotionType Type { get; private set; }
    public int? ProductAId { get; private set; }
    public Product? ProductA { get; private set; }
    public int? QtyA { get; private set; }
    public int? ProductBId { get; private set; }
    public Product? ProductB { get; private set; }
    public int? QtyB { get; private set; }
    public int? FreeProductId { get; private set; }
    public Product? FreeProduct { get; private set; }
    public int? FreeQty { get; private set; }
    public int? RatePercent { get; private set; }
    public decimal? MinSubtotal { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PromotionVersion() { } // EF

    internal PromotionVersion(int promotionId, PromotionConditions c, int createdById, DateTimeOffset createdAt)
    {
        PromotionId = promotionId;
        Name = c.Name;
        Type = c.Type;
        ProductAId = c.ProductAId;
        QtyA = c.QtyA;
        ProductBId = c.ProductBId;
        QtyB = c.QtyB;
        FreeProductId = c.FreeProductId;
        FreeQty = c.FreeQty;
        RatePercent = c.RatePercent;
        MinSubtotal = c.MinSubtotal;
        StartDate = c.StartDate;
        EndDate = c.EndDate;
        CreatedById = createdById;
        CreatedAt = createdAt;
    }
}

/// <summary>ENT-006 attribute names — the field a message sits under.</summary>
public static class PromotionFields
{
    public const string Name = "name";
    public const string Type = "type";
    public const string ProductA = "productA";
    public const string QtyA = "qtyA";
    public const string ProductB = "productB";
    public const string QtyB = "qtyB";
    public const string FreeProduct = "freeProduct";
    public const string FreeQty = "freeQty";
    public const string RatePercent = "ratePercent";
    public const string MinSubtotal = "minSubtotal";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
}

/// <summary>
/// UI-talad-016 state "error" names the kinds of fault but gives no wording, so these sentences are dev's
/// (FE-talad-025) — one per kind, reused under whichever field has it.
/// </summary>
public static class PromotionMessages
{
    public const string Required = "กรุณากรอกช่องนี้";
    public const string TypeRequired = "กรุณาเลือกรูปแบบโปรโมชั่น";
    public const string ProductNotSellable = "ไม่พบสินค้านี้ หรือสินค้าเลิกขายแล้ว";
    public const string QtyAtLeastOne = "จำนวนต่อชุดต้องเป็นจำนวนเต็มมากกว่า 0";
    public const string RateOutOfRange = "ส่วนลดต้องเป็นจำนวนเต็ม 0–100";
    public const string MinSubtotalInvalid = "ยอดขั้นต่ำต้องไม่ติดลบ และมีทศนิยมไม่เกิน 2 ตำแหน่ง";
    public const string StartRequired = "กรุณาระบุวันเริ่ม";
    public const string DateInvalid = "วันที่ไม่ถูกต้อง";
    public const string EndBeforeStart = "วันสิ้นสุดต้องไม่ก่อนวันเริ่ม";
}

public sealed record PromotionFieldError(string Field, string Message);

/// <summary>Every field that is wrong, not just the first.</summary>
public sealed class PromotionInvalidException(IReadOnlyList<PromotionFieldError> errors)
    : Exception(string.Join(" · ", errors.Select(e => $"{e.Field}: {e.Message}")))
{
    public IReadOnlyList<PromotionFieldError> Errors { get; } = errors;
}

/// <summary>ACL-023 · ENT-006.createdBy — only the owner creates or edits a promotion.</summary>
public sealed class PromotionOwnerOnlyException() : Exception("only the owner may create or edit a promotion");

/// <summary>STM-talad-002 — a discontinued promotion is not edited.</summary>
public sealed class PromotionNotActiveException(int promotionId) : Exception($"promotion {promotionId} is discontinued");
