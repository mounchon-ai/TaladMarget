using Talad.Domain.Members;

namespace Talad.Application.Members;

public interface IMemberRepository
{
    /// <summary>BR-talad-030@v1 — is this (already normalized) phone an ACTIVE member's? Hidden members do not count.</summary>
    Task<bool> ActivePhoneExistsAsync(string phone, CancellationToken ct);
    void Add(Member member);

    /// <summary>Throws <see cref="MemberPhoneTakenException"/> when the database's own unique index refuses the phone.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record MemberView(int Id, string Name, string Phone, decimal AccumulatedAmount, string Status);

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
        return new MemberView(member.Id, member.Name, member.Phone, member.AccumulatedAmount, member.Status.ToString());
    }
}
