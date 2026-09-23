using Talad.Domain.Accounts;

namespace Talad.Domain.Settings;

/// <summary>
/// ENT-007 · เวอร์ชันส่วนลดสมาชิก. One % for the whole shop, set by the owner (BR-talad-010@v1 · ACL-025).
/// Every change is a new row and the newest is the one in force; a bill points at the row it was sold
/// under, so old bills never change (BR-talad-035@v1 · BR-talad-036@v1). No row at all means 0%.
/// </summary>
public class MemberDiscountVersion
{
    public const string Field = "ratePercent";
    public const string RateOutOfRange = "ส่วนลดสมาชิกต้องเป็นจำนวนเต็ม 0–100";

    public int Id { get; private set; }
    public int RatePercent { get; private set; }
    public int ChangedById { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private MemberDiscountVersion() { } // EF

    private MemberDiscountVersion(int ratePercent, int changedById, DateTimeOffset changedAt)
    {
        RatePercent = ratePercent;
        ChangedById = changedById;
        ChangedAt = changedAt;
    }

    /// <summary>UC-talad-023 — a whole % from 0 to 100, set by the owner; anything else is refused.</summary>
    public static MemberDiscountVersion Set(decimal? ratePercent, UserAccount by, DateTimeOffset at)
    {
        if (by.Role != UserRole.Owner) throw new MemberDiscountOwnerOnlyException();
        if (ratePercent is not { } rate || rate != decimal.Truncate(rate) || rate < 0 || rate > 100)
            throw new MemberDiscountRateException();
        return new MemberDiscountVersion((int)rate, by.Id, at);
    }
}

/// <summary>BR-talad-010@v1 — the rate is a whole % from 0 to 100.</summary>
public sealed class MemberDiscountRateException() : Exception(MemberDiscountVersion.RateOutOfRange);

/// <summary>ACL-025 · ENT-007.changedBy — only the owner sets the member discount.</summary>
public sealed class MemberDiscountOwnerOnlyException() : Exception("only the owner may set the member discount");
