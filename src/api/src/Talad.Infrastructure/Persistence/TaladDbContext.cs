using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Domain.Accounts;

namespace Talad.Infrastructure.Persistence;

public class TaladDbContext(DbContextOptions<TaladDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserAccountConfiguration());
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.Username).HasColumnName("username").IsRequired();
        // ENT-008.username — unique across every account, disabled ones included
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().IsRequired();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
