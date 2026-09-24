using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Sales;

namespace Talad.Infrastructure.Persistence;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            // BR-talad-007@v1 · enforced at db as well as domain (interfaces.json ruleEnforcement)
            t.HasCheckConstraint("ck_products_stock_qty", "stock_qty >= 0");
            t.HasCheckConstraint("ck_products_low_stock_threshold", "low_stock_threshold >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Barcode).HasColumnName("barcode");
        // ENT-001.barcode — unique among ACTIVE products only
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("barcode IS NOT NULL AND status = 'Active'");
        b.Property(x => x.ImagePath).HasColumnName("image_path");
        b.Property(x => x.CurrentPriceVersionId).HasColumnName("current_price_version_id");
        b.HasOne(x => x.CurrentPriceVersion).WithMany().HasForeignKey(x => x.CurrentPriceVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.StockQty).HasColumnName("stock_qty");
        b.Property(x => x.LowStockThreshold).HasColumnName("low_stock_threshold");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.IsActive);
        b.Ignore(x => x.IsLowStock);
        b.Ignore(x => x.CurrentPrice);
    }
}

internal sealed class ProductPriceVersionConfiguration : IEntityTypeConfiguration<ProductPriceVersion>
{
    public void Configure(EntityTypeBuilder<ProductPriceVersion> b)
    {
        b.ToTable("product_price_versions", t => t.HasCheckConstraint("ck_product_price_versions_price", "price > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Price).HasColumnName("price").HasPrecision(12, 2);
        b.Property(x => x.PreviousPrice).HasColumnName("previous_price").HasPrecision(12, 2);
        b.Property(x => x.Source).HasColumnName("source").HasConversion<string>().IsRequired();
        b.Property(x => x.ChangedById).HasColumnName("changed_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ChangedAt).HasColumnName("changed_at");
    }
}

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    /// <summary>BR-talad-041@v1 at db — the name <see cref="ProductRepository"/> looks for in a 23505.</summary>
    public const string RequestKeyIndex = "ix_stock_adjustments_request_key";

    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.ToTable("stock_adjustments", t =>
        {
            // ENT-003 · BR-talad-032@v1 at db as well as domain (interfaces.json ruleEnforcement); the rows are
            // insert-only through the trigger of migration AddStockAdjustments
            t.HasCheckConstraint("ck_stock_adjustments_quantity_delta", "quantity_delta <> 0");
            t.HasCheckConstraint("ck_stock_adjustments_counted_qty", "counted_qty IS NULL OR counted_qty >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasColumnName("reason").HasConversion<string>().IsRequired();
        b.Property(x => x.QuantityDelta).HasColumnName("quantity_delta");
        b.Property(x => x.CountedQty).HasColumnName("counted_qty");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.RequestKey).HasColumnName("request_key").IsRequired();
        b.HasIndex(x => x.RequestKey).IsUnique().HasDatabaseName(RequestKeyIndex);
        b.Property(x => x.AdjustedById).HasColumnName("adjusted_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AdjustedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.AdjustedAt).HasColumnName("adjusted_at");
        // UI-talad-012 adjustments section — one product's rows newest first, 20 a page (NFR-talad-006)
        b.HasIndex(x => new { x.ProductId, x.AdjustedAt, x.Id }).HasDatabaseName("ix_stock_adjustments_product_adjusted_at");
    }
}

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("carts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        // ENT-009.owner — one OPEN cart per person
        b.HasIndex(x => x.OwnerId).IsUnique().HasFilter("status = 'Open'");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.Property(x => x.OpenedAt).HasColumnName("opened_at");
        // ENT-009.member — optional; a member is never deleted, so the cart never loses them (BR-talad-040@v2)
        b.Property(x => x.MemberId).HasColumnName("member_id");
        b.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
{
    public void Configure(EntityTypeBuilder<CartLine> b)
    {
        b.ToTable("cart_lines", t => t.HasCheckConstraint("ck_cart_lines_qty", "qty >= 1"));
        b.HasKey(x => new { x.CartId, x.ProductId }); // ENT-010 key: cart + product
        b.Property(x => x.CartId).HasColumnName("cart_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Qty).HasColumnName("qty");
        b.Property(x => x.AddedAt).HasColumnName("added_at");
    }
}
