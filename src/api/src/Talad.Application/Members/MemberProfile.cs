using Talad.Domain.Members;

namespace Talad.Application.Members;

public sealed class MemberNotFoundException(int memberId) : Exception($"member {memberId} does not exist or is hidden");

/// <summary>
/// UC-talad-008 · API-014 · API-015 — one member, and a new name or phone for them. Any signed-in seller or
/// owner may edit any ACTIVE member (ACL-008 · BR-talad-031@v1); a hidden member is not found
/// (BR-talad-040@v2 · UI-talad-006 state "error": ไม่พบสมาชิก).
/// </summary>
public sealed class MemberProfile(IMemberRepository members)
{
    /// <summary>API-014</summary>
    public async Task<MemberView> GetAsync(int id, CancellationToken ct = default) =>
        MemberView.Of(await ActiveAsync(id, ct));

    /// <summary>API-015 — both fields are sent; nothing changes when either is refused.</summary>
    public async Task<MemberView> EditAsync(int id, string? name, string? phone, CancellationToken ct = default)
    {
        var member = await ActiveAsync(id, ct);
        member.Edit(name, phone);
        if (await members.ActivePhoneExistsAsync(member.Phone, ct, exceptMemberId: member.Id)) throw new MemberPhoneTakenException();
        await members.SaveChangesAsync(ct);
        return MemberView.Of(member);
    }

    private async Task<Member> ActiveAsync(int id, CancellationToken ct)
    {
        var member = await members.FindAsync(id, ct);
        return member is { Status: MemberStatus.Active } ? member : throw new MemberNotFoundException(id);
    }
}
