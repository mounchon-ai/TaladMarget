using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talad.Application.Members;
using Talad.Domain.Members;

namespace Talad.Infrastructure.Persistence;

internal sealed class MemberRepository(TaladDbContext db) : IMemberRepository
{
    public Task<bool> ActivePhoneExistsAsync(string phone, CancellationToken ct, int? exceptMemberId = null) =>
        db.Members.AnyAsync(m => m.Phone == phone && m.Status == MemberStatus.Active && m.Id != exceptMemberId, ct);

    public async Task<(IReadOnlyList<Member> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Members.Where(m => m.Status == MemberStatus.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            // BR-talad-004@v1 — the whole phone (equality, never a part of it), or part of the name
            var t = term.Trim();
            var phone = Member.NormalizePhone(t);
            var lowered = t.ToLower();
            query = query.Where(m => m.Phone == phone || m.Name.ToLower().Contains(lowered));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.Name).ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Member?> FindAsync(int id, CancellationToken ct) => db.Members.SingleOrDefaultAsync(m => m.Id == id, ct);

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
