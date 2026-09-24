using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Members;
using Talad.Domain.Promotions;
using Talad.Domain.Sales;
using Talad.Domain.Settings;

namespace Talad.Infrastructure.Persistence;

public class TaladDbContext(DbContextOptions<TaladDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPriceVersion> ProductPriceVersions => Set<ProductPriceVersion>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberDiscountVersion> MemberDiscountVersions => Set<MemberDiscountVersion>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionVersion> PromotionVersions => Set<PromotionVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserAccountConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductPriceVersionConfiguration());
        modelBuilder.ApplyConfiguration(new StockAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new CartConfiguration());
        modelBuilder.ApplyConfiguration(new CartLineConfiguration());
        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new MemberDiscountVersionConfiguration());
        modelBuilder.ApplyConfiguration(new PromotionConfiguration());
        modelBuilder.ApplyConfiguration(new PromotionVersionConfiguration());
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
