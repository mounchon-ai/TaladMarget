using Talad.Domain.Members;

namespace Talad.Application.Members;

public interface IMemberRepository
{
    /// <summary>
    /// BR-talad-030@v1 — is this (already normalized) phone an ACTIVE member's? Hidden members do not count,
    /// and neither does <paramref name="exceptMemberId"/> — the member being edited keeps their own phone.
    /// </summary>
    Task<bool> ActivePhoneExistsAsync(string phone, CancellationToken ct, int? exceptMemberId = null);

    /// <summary>
    /// API-012 · BR-talad-004@v1 — ACTIVE members whose phone is the whole (normalized) term, or whose name
    /// contains it; an empty term is every ACTIVE member. Never a part of a phone. Ordered by name.
    /// </summary>
    Task<(IReadOnlyList<Member> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct);

    /// <summary>The member with this id, whatever their status.</summary>
    Task<Member?> FindAsync(int id, CancellationToken ct);
    void Add(Member member);

    /// <summary>Throws <see cref="MemberPhoneTakenException"/> when the database's own unique index refuses the phone.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record MemberView(int Id, string Name, string Phone, decimal AccumulatedAmount, string Status)
{
    public static MemberView Of(Member m) => new(m.Id, m.Name, m.Phone, m.AccumulatedAmount, m.Status.ToString());
}

/// <summary>
/// UC-talad-007 · API-013. Any signed-in seller or owner may register (ACL-007 · BR-talad-021@v1); the
/// registrar is the caller, never a value from the request.
/// </summary>
public sealed class MemberRegistration(IMemberRepository members, TimeProvider clock)
{
    public async Task<MemberView> RegisterAsync(string? name, string? phone, int registrarId, CancellationToken ct = default)
    {
        var member = Member.Register(name, phone, registrarId, clock.GetUtcNow());
        if (await members.ActivePhoneExistsAsync(member.Phone, ct)) throw new MemberPhoneTakenException();
        members.Add(member);
        await members.SaveChangesAsync(ct);
        return MemberView.Of(member);
    }
}
