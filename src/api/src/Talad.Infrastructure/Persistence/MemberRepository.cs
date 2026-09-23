using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talad.Application.Members;
using Talad.Domain.Members;

namespace Talad.Infrastructure.Persistence;

internal sealed class MemberRepository(TaladDbContext db) : IMemberRepository
{
    public Task<bool> ActivePhoneExistsAsync(string phone, CancellationToken ct) =>
        db.Members.AnyAsync(m => m.Phone == phone && m.Status == MemberStatus.Active, ct);

    public void Add(Member member) => db.Members.Add(member);

    /// <summary>
    /// Two saves of the same phone can both pass the ACTIVE check before either commits (a double click);
    /// the partial unique index turns the second away, and it answers the same as the check would have.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: MemberConfiguration.ActivePhoneIndex,
        })
        {
            throw new MemberPhoneTakenException();
        }
    }
}
