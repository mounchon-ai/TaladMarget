namespace Talad.Domain.Members;

/// <summary>
/// ENT-004 · สมาชิก. Status follows STM-talad-003 — a member is never deleted, only hidden, and a hidden
/// member's phone is free again (BR-talad-040@v2). Registering always makes a new member at ฿0, even on a
/// phone a hidden member once held; the hidden row is never brought back.
/// </summary>
public class Member
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Phone { get; private set; } = null!;

    /// <summary>ENT-004.accumulatedAmount — starts at 0; moved only by paying and voiding bills (BR-talad-003@v1).</summary>
    public decimal AccumulatedAmount { get; private set; }

    public MemberStatus Status { get; private set; }
    public int CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int? HiddenById { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }

    private Member() { } // EF

    private Member(string name, string phone, int createdById, DateTimeOffset createdAt)
    {
        Name = name;
        Phone = phone;
        AccumulatedAmount = 0m;
        Status = MemberStatus.Active; // STM-talad-003 initial
        CreatedById = createdById;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// UC-talad-007 — a new ACTIVE member at ฿0, or every field that is wrong (BR-talad-002@v1).
    /// Whether the phone is already an ACTIVE member's (BR-talad-030@v1) needs the other members, so the
    /// caller asks that before this.
    /// </summary>
    public static Member Register(string? name, string? phone, int createdById, DateTimeOffset createdAt)
    {
        var cleanName = (name ?? "").Trim();
        var cleanPhone = NormalizePhone(phone);
        var errors = new List<MemberFieldError>();
        if (cleanName.Length == 0) errors.Add(new(MemberField.Name, MemberMessages.NameRequired));
        if (!IsValidPhone(cleanPhone)) errors.Add(new(MemberField.Phone, MemberMessages.PhoneFormat));
        if (errors.Count > 0) throw new MemberInvalidException(errors);
        return new Member(cleanName, cleanPhone, createdById, createdAt);
    }

    /// <summary>ENT-004.phone — dashes and spaces come out before checking and before storing.</summary>
    public static string NormalizePhone(string? phone) =>
        string.Concat((phone ?? "").Where(c => c != '-' && !char.IsWhiteSpace(c)));

    /// <summary>
    /// BR-talad-002@v1 — ten digits, the first a 0. Only ASCII 0-9: char.IsDigit and \d also take Thai
    /// digits (๐-๙), which a phone number here never is.
    /// </summary>
    public static bool IsValidPhone(string phone) =>
        phone.Length == 10 && phone[0] == '0' && phone.All(c => c is >= '0' and <= '9');
}

/// <summary>STM-talad-003</summary>
public enum MemberStatus
{
    Active,
    Hidden,
}

/// <summary>ENT-004 attribute names — the field a message sits under.</summary>
public static class MemberField
{
    public const string Name = "name";
    public const string Phone = "phone";
}

/// <summary>The sentences UI-talad-005 declares, word for word.</summary>
public static class MemberMessages
{
    public const string NameRequired = "กรุณากรอกชื่อ";
    public const string PhoneFormat = "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0";
    public const string PhoneTaken = "เบอร์โทรนี้เป็นสมาชิกอยู่แล้ว";
}

public sealed record MemberFieldError(string Field, string Message);

public abstract class MemberRuleException(IReadOnlyList<MemberFieldError> errors)
    : Exception(string.Join(" · ", errors.Select(e => e.Message)))
{
    public IReadOnlyList<MemberFieldError> Errors { get; } = errors;
}

/// <summary>BR-talad-002@v1 — every field that is wrong, not just the first.</summary>
public sealed class MemberInvalidException(IReadOnlyList<MemberFieldError> errors) : MemberRuleException(errors);

/// <summary>BR-talad-030@v1 — the phone is an ACTIVE member's already (a hidden member's does not count).</summary>
public sealed class MemberPhoneTakenException()
    : MemberRuleException([new MemberFieldError(MemberField.Phone, MemberMessages.PhoneTaken)]);
