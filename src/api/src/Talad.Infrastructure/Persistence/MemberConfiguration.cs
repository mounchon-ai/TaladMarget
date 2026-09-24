using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Domain.Accounts;
using Talad.Domain.Members;

namespace Talad.Infrastructure.Persistence;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    /// <summary>BR-talad-030@v1 · BR-talad-040@v2 — the name <see cref="MemberRepository"/> looks for in a 23505.</summary>
    public const string ActivePhoneIndex = "ix_members_phone_active";

    public void Configure(EntityTypeBuilder<Member> b)
    {
        b.ToTable("members", t =>
        {
            // BR-talad-003@v1 · enforced at db as well as domain (interfaces.json ruleEnforcement)
            t.HasCheckConstraint("ck_members_accumulated_amount", "accumulated_amount >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(10).IsRequired();
        // ENT-004.phone — one ACTIVE member per phone; a hidden member's phone is free again
        b.HasIndex(x => x.Phone).IsUnique().HasFilter("status = 'Active'").HasDatabaseName(ActivePhoneIndex);
        // BR-talad-003@v1 — two sales adding to one member: the second save finds the amount moved and is run again
        b.Property(x => x.AccumulatedAmount).HasColumnName("accumulated_amount").HasPrecision(12, 2).IsConcurrencyToken();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.Property(x => x.CreatedById).HasColumnName("created_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.HiddenById).HasColumnName("hidden_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.HiddenById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.HiddenAt).HasColumnName("hidden_at");
    }
}
