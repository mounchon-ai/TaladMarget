using Talad.Application.Auth;
using Talad.Domain.Members;

namespace Talad.Application.Members;

public sealed class MemberNotFoundException(int memberId) : Exception($"member {memberId} does not exist or is hidden");

/// <summary>
/// UC-talad-008 · API-014 · API-015 — one member, and a new name or phone for them. Any signed-in seller or
/// owner may edit any ACTIVE member (ACL-008 · BR-talad-031@v1); a hidden member is not found
/// (BR-talad-040@v2 · UI-talad-006 state "error": ไม่พบสมาชิก).
/// </summary>
public sealed class MemberProfile(IMemberRepository members, IUserAccountRepository accounts, TimeProvider clock, IUnitOfWork work)
{
    /// <summary>API-014</summary>
    public async Task<MemberView> GetAsync(int id, CancellationToken ct = default) =>
        MemberView.Of(await ActiveAsync(id, ct));

    /// <summary>API-015 — both fields are sent; nothing changes when either is refused.</summary>
    public Task<MemberView> EditAsync(int id, string? name, string? phone, CancellationToken ct = default) =>
        Conflicts.RetryAsync(work, () => EditOnceAsync(id, name, phone, ct)); // a sale can add to the member meanwhile

    private async Task<MemberView> EditOnceAsync(int id, string? name, string? phone, CancellationToken ct)
    {
        var member = await ActiveAsync(id, ct);
        member.Edit(name, phone);
        if (await members.ActivePhoneExistsAsync(member.Phone, ct, exceptMemberId: member.Id)) throw new MemberPhoneTakenException();
        await members.SaveChangesAsync(ct);
        return MemberView.Of(member);
    }

    /// <summary>
    /// API-016 — hide an ACTIVE member (UC-talad-010). The caller's role is read from their account, not
    /// taken on the token's word, and the domain refuses anyone but the owner (BR-talad-019@v1).
    /// </summary>
    public Task HideAsync(int id, int callerId, CancellationToken ct = default) =>
        Conflicts.RetryAsync(work, () => HideOnceAsync(id, callerId, ct));

    private async Task HideOnceAsync(int id, int callerId, CancellationToken ct)
    {
        var member = await ActiveAsync(id, ct);
        var caller = await accounts.FindByIdAsync(callerId, ct) ?? throw new OwnerOnlyException();
        member.Hide(caller, clock.GetUtcNow());
        await members.SaveChangesAsync(ct);
    }

    private async Task<Member> ActiveAsync(int id, CancellationToken ct)
    {
        var member = await members.FindAsync(id, ct);
        return member is { Status: MemberStatus.Active } ? member : throw new MemberNotFoundException(id);
    }
}
