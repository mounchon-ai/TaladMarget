using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Members;
using Talad.Domain.Promotions;
using Talad.Domain.Settings;
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
        // BR-talad-007@v1 · BR-talad-032@v1 — a sale and an adjustment moving the same stock: the second save finds it
        // moved and is run again from a fresh read (FE-talad-033), never written over
        b.Property(x => x.StockQty).HasColumnName("stock_qty").IsConcurrencyToken();
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

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    /// <summary>BR-talad-039@v1 at db — one bill per cart; the name <see cref="SaleRepository"/> looks for in a 23505.</summary>
    public const string CartIndex = "ix_sales_cart_id";

    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("sales", t =>
        {
            // ENT-011 — money is numeric(12,2) and never below zero; a VOIDED bill carries who, when and why
            t.HasCheckConstraint("ck_sales_amounts", "subtotal >= 0 AND promo_discount_total >= 0 AND bill_discount >= 0 AND member_discount >= 0 AND net_total >= 0");
            t.HasCheckConstraint("ck_sales_void", "status <> 'Voided' OR (voided_by_id IS NOT NULL AND voided_at IS NOT NULL AND length(trim(void_reason)) > 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.ReceiptNo).HasColumnName("receipt_no").IsRequired();
        // UI-talad-007 — found by its receipt number
        b.HasIndex(x => x.ReceiptNo).IsUnique().HasDatabaseName("ix_sales_receipt_no");
        b.Property(x => x.CartId).HasColumnName("cart_id");
        b.HasOne<Cart>().WithMany().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CartId).IsUnique().HasDatabaseName(CartIndex);
        b.Property(x => x.SellerId).HasColumnName("seller_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.MemberId).HasColumnName("member_id");
        b.HasOne<Member>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PaidAt).HasColumnName("paid_at");
        b.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        b.Property(x => x.PromoDiscountTotal).HasColumnName("promo_discount_total").HasPrecision(12, 2);
        b.Property(x => x.BillPromotionVersionId).HasColumnName("bill_promotion_version_id");
        b.HasOne<PromotionVersion>().WithMany().HasForeignKey(x => x.BillPromotionVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.BillDiscount).HasColumnName("bill_discount").HasPrecision(12, 2);
        b.Property(x => x.MemberDiscountVersionId).HasColumnName("member_discount_version_id");
        b.HasOne<MemberDiscountVersion>().WithMany().HasForeignKey(x => x.MemberDiscountVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.MemberDiscount).HasColumnName("member_discount").HasPrecision(12, 2);
        b.Property(x => x.NetTotal).HasColumnName("net_total").HasPrecision(12, 2);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        b.Property(x => x.VoidedById).HasColumnName("voided_by_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.VoidedById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.VoidedAt).HasColumnName("voided_at");
        b.Property(x => x.VoidReason).HasColumnName("void_reason");
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        // UI-talad-007 · RPT-talad-001..004 — history and reports by date (NFR-talad-006..008), by seller, by member
        b.HasIndex(x => new { x.PaidAt, x.Id }).HasDatabaseName("ix_sales_paid_at");
        b.HasIndex(x => new { x.Status, x.PaidAt }).HasDatabaseName("ix_sales_status_paid_at");
        b.HasIndex(x => new { x.SellerId, x.PaidAt }).HasDatabaseName("ix_sales_seller_paid_at");
        b.HasIndex(x => x.MemberId).HasDatabaseName("ix_sales_member_id");
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("sale_lines", t =>
        {
            // ENT-012 — qty at least 1, the free pieces 0..qty, money never below zero
            t.HasCheckConstraint("ck_sale_lines_qty", "qty >= 1 AND free_qty >= 0 AND free_qty <= qty");
            t.HasCheckConstraint("ck_sale_lines_amounts", "unit_price >= 0 AND promo_discount >= 0 AND line_net >= 0");
        });
        b.HasKey(x => new { x.SaleId, x.LineNo });
        b.Property(x => x.SaleId).HasColumnName("sale_id");
        b.Property(x => x.LineNo).HasColumnName("line_no");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        // BR-talad-036@v1 at db — a version a bill points at cannot be deleted
        b.Property(x => x.PriceVersionId).HasColumnName("price_version_id");
        b.HasOne<ProductPriceVersion>().WithMany().HasForeignKey(x => x.PriceVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
        b.Property(x => x.Qty).HasColumnName("qty");
        b.Property(x => x.FreeQty).HasColumnName("free_qty");
        b.Property(x => x.PromotionVersionId).HasColumnName("promotion_version_id");
        b.HasOne<PromotionVersion>().WithMany().HasForeignKey(x => x.PromotionVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PromoDiscount).HasColumnName("promo_discount").HasPrecision(12, 2);
        b.Property(x => x.LineNet).HasColumnName("line_net").HasPrecision(12, 2);
        // RPT-talad-003 best sellers — lines by product
        b.HasIndex(x => x.ProductId).HasDatabaseName("ix_sale_lines_product_id");
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
