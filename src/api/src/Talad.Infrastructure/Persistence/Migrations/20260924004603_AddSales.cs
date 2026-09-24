using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ENT-011 · ENT-012 — the bill and its lines (FE-talad-033). BR-talad-025@v1 at db: a line is never changed or
    /// deleted, and a bill changes only in its void fields (the void unit's); BR-talad-039@v1 at db: one bill per cart;
    /// BR-talad-036@v1 at db: every version a bill points at is a restrict foreign key. Also the concurrency checks on
    /// products.stock_qty and members.accumulated_amount, which change the model but no column.
    /// </summary>
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    receipt_no = table.Column<string>(type: "text", nullable: false),
                    cart_id = table.Column<int>(type: "integer", nullable: false),
                    seller_id = table.Column<int>(type: "integer", nullable: false),
                    member_id = table.Column<int>(type: "integer", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    promo_discount_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bill_promotion_version_id = table.Column<int>(type: "integer", nullable: true),
                    bill_discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    member_discount_version_id = table.Column<int>(type: "integer", nullable: true),
                    member_discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    net_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    voided_by_id = table.Column<int>(type: "integer", nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.id);
                    table.CheckConstraint("ck_sales_amounts", "subtotal >= 0 AND promo_discount_total >= 0 AND bill_discount >= 0 AND member_discount >= 0 AND net_total >= 0");
                    table.CheckConstraint("ck_sales_void", "status <> 'Voided' OR (voided_by_id IS NOT NULL AND voided_at IS NOT NULL AND length(trim(void_reason)) > 0)");
                    table.ForeignKey(
                        name: "FK_sales_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_member_discount_versions_member_discount_version_id",
                        column: x => x.member_discount_version_id,
                        principalTable: "member_discount_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_promotion_versions_bill_promotion_version_id",
                        column: x => x.bill_promotion_version_id,
                        principalTable: "promotion_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_user_accounts_seller_id",
                        column: x => x.seller_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_user_accounts_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_lines",
                columns: table => new
                {
                    sale_id = table.Column<int>(type: "integer", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    price_version_id = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    free_qty = table.Column<int>(type: "integer", nullable: false),
                    promotion_version_id = table.Column<int>(type: "integer", nullable: true),
                    promo_discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    line_net = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_lines", x => new { x.sale_id, x.line_no });
                    table.CheckConstraint("ck_sale_lines_amounts", "unit_price >= 0 AND promo_discount >= 0 AND line_net >= 0");
                    table.CheckConstraint("ck_sale_lines_qty", "qty >= 1 AND free_qty >= 0 AND free_qty <= qty");
                    table.ForeignKey(
                        name: "FK_sale_lines_product_price_versions_price_version_id",
                        column: x => x.price_version_id,
                        principalTable: "product_price_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_lines_promotion_versions_promotion_version_id",
                        column: x => x.promotion_version_id,
                        principalTable: "promotion_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_lines_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_price_version_id",
                table: "sale_lines",
                column: "price_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_lines_product_id",
                table: "sale_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_promotion_version_id",
                table: "sale_lines",
                column: "promotion_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_promotion_version_id",
                table: "sales",
                column: "bill_promotion_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_cart_id",
                table: "sales",
                column: "cart_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_member_discount_version_id",
                table: "sales",
                column: "member_discount_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_member_id",
                table: "sales",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_paid_at",
                table: "sales",
                columns: new[] { "paid_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_receipt_no",
                table: "sales",
                column: "receipt_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_seller_paid_at",
                table: "sales",
                columns: new[] { "seller_id", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_status_paid_at",
                table: "sales",
                columns: new[] { "status", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_voided_by_id",
                table: "sales",
                column: "voided_by_id");

            migrationBuilder.Sql("""
                CREATE FUNCTION sale_lines_insert_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'sale_lines is insert-only (BR-talad-025): % refused', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER sale_lines_insert_only
                    BEFORE UPDATE OR DELETE ON sale_lines
                    FOR EACH ROW EXECUTE FUNCTION sale_lines_insert_only();

                CREATE FUNCTION sales_void_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'sales rows are never deleted (BR-talad-025)' USING ERRCODE = 'restrict_violation';
                    END IF;
                    IF (NEW.id, NEW.receipt_no, NEW.cart_id, NEW.seller_id, NEW.member_id, NEW.paid_at, NEW.subtotal,
                        NEW.promo_discount_total, NEW.bill_promotion_version_id, NEW.bill_discount,
                        NEW.member_discount_version_id, NEW.member_discount, NEW.net_total)
                       IS DISTINCT FROM
                       (OLD.id, OLD.receipt_no, OLD.cart_id, OLD.seller_id, OLD.member_id, OLD.paid_at, OLD.subtotal,
                        OLD.promo_discount_total, OLD.bill_promotion_version_id, OLD.bill_discount,
                        OLD.member_discount_version_id, OLD.member_discount, OLD.net_total) THEN
                        RAISE EXCEPTION 'a paid bill changes only in its void fields (BR-talad-025)' USING ERRCODE = 'restrict_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER sales_void_only
                    BEFORE UPDATE OR DELETE ON sales
                    FOR EACH ROW EXECUTE FUNCTION sales_void_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER sales_void_only ON sales;
                DROP FUNCTION sales_void_only();
                DROP TRIGGER sale_lines_insert_only ON sale_lines;
                DROP FUNCTION sale_lines_insert_only();
                """);
            migrationBuilder.DropTable(
                name: "sale_lines");

            migrationBuilder.DropTable(
                name: "sales");
        }
    }
}
