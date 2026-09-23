using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Application.Settings;
using Talad.Domain.Accounts;
using Talad.Domain.Settings;

namespace Talad.Infrastructure.Persistence;

internal sealed class MemberDiscountVersionConfiguration : IEntityTypeConfiguration<MemberDiscountVersion>
{
    public void Configure(EntityTypeBuilder<MemberDiscountVersion> b)
    {
        // BR-talad-010@v1 · enforced at db as well as domain
        b.ToTable("member_discount_versions", t => t.HasCheckConstraint("ck_member_discount_versions_rate", "rate_percent BETWEEN 0 AND 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.RatePercent).HasColumnName("rate_percent");
        b.Property(x => x.ChangedById).HasColumnName("changed_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ChangedAt).HasColumnName("changed_at");
    }
}

internal sealed class MemberDiscountRepository(TaladDbContext db) : IMemberDiscountRepository
{
    public async Task<(MemberDiscountVersion Version, string ChangedByName)?> LatestAsync(CancellationToken ct)
    {
        var latest = await db.MemberDiscountVersions
            .OrderByDescending(v => v.Id)
            .Join(db.UserAccounts, v => v.ChangedById, a => a.Id, (v, a) => new { Version = v, a.DisplayName })
            .FirstOrDefaultAsync(ct);
        return latest is null ? null : (latest.Version, latest.DisplayName);
    }

    public void Add(MemberDiscountVersion version) => db.MemberDiscountVersions.Add(version);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
