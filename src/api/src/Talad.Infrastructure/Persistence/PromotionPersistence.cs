using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Application.Promotions;
using Talad.Domain.Accounts;
using Talad.Domain.Promotions;

namespace Talad.Infrastructure.Persistence;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("promotions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        // the promotion and its versions point at each other, so this side is saved second and may be empty
        b.Property(x => x.CurrentVersionId).HasColumnName("current_version_id");
        b.HasOne(x => x.CurrentVersion).WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.HasIndex(x => x.Status);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class PromotionVersionConfiguration : IEntityTypeConfiguration<PromotionVersion>
{
    public void Configure(EntityTypeBuilder<PromotionVersion> b)
    {
        // BR-talad-009 · 011..015 — enforced at db as well as domain
        b.ToTable("promotion_versions", t =>
        {
            t.HasCheckConstraint("ck_promotion_versions_rate", "rate_percent IS NULL OR rate_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_promotion_versions_qty_a", "qty_a IS NULL OR qty_a >= 1");
            t.HasCheckConstraint("ck_promotion_versions_qty_b", "qty_b IS NULL OR qty_b >= 1");
            t.HasCheckConstraint("ck_promotion_versions_free_qty", "free_qty IS NULL OR free_qty >= 1");
            t.HasCheckConstraint("ck_promotion_versions_min_subtotal", "min_subtotal IS NULL OR min_subtotal >= 0");
            t.HasCheckConstraint("ck_promotion_versions_dates", "end_date IS NULL OR end_date >= start_date");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.PromotionId).HasColumnName("promotion_id");
        b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        // stored as design's own code (ITEM_PERCENT …) so the table reads the way the design does
        b.Property(x => x.Type).HasColumnName("type").IsRequired()
            .HasConversion(t => t.Code(), s => PromotionTypes.Parse(s)!.Value);
        b.Property(x => x.ProductAId).HasColumnName("product_a_id");
        b.HasOne(x => x.ProductA).WithMany().HasForeignKey(x => x.ProductAId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.QtyA).HasColumnName("qty_a");
        b.Property(x => x.ProductBId).HasColumnName("product_b_id");
        b.HasOne(x => x.ProductB).WithMany().HasForeignKey(x => x.ProductBId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.QtyB).HasColumnName("qty_b");
        b.Property(x => x.FreeProductId).HasColumnName("free_product_id");
        b.HasOne(x => x.FreeProduct).WithMany().HasForeignKey(x => x.FreeProductId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FreeQty).HasColumnName("free_qty");
        b.Property(x => x.RatePercent).HasColumnName("rate_percent");
        b.Property(x => x.MinSubtotal).HasColumnName("min_subtotal").HasPrecision(12, 2);
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.EndDate).HasColumnName("end_date");
        b.Property(x => x.CreatedById).HasColumnName("created_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class PromotionRepository(TaladDbContext db) : IPromotionRepository
{
    private IQueryable<Promotion> WithVersion() =>
        db.Promotions
            .Include(p => p.CurrentVersion).ThenInclude(v => v!.ProductA)
            .Include(p => p.CurrentVersion).ThenInclude(v => v!.ProductB)
            .Include(p => p.CurrentVersion).ThenInclude(v => v!.FreeProduct);

    public async Task<(IReadOnlyList<Promotion> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var query = WithVersion().Where(p => p.Status == PromotionStatus.Active && p.CurrentVersionId != null);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowered = term.Trim().ToLower();
            query = query.Where(p => p.CurrentVersion!.Name.ToLower().Contains(lowered));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.CurrentVersion!.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Promotion?> FindAsync(int id, CancellationToken ct) => WithVersion().SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Add(Promotion promotion) => db.Promotions.Add(promotion);

    public void Add(PromotionVersion version) => db.PromotionVersions.Add(version);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
