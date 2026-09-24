using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ENT-003 · รายการปรับสต็อก (FE-talad-023). BR-talad-032@v1 at db: rows are insert-only (the trigger below) and a
    /// product's stock never goes below zero (ck_products_stock_qty, already there). BR-talad-041@v1 at db: the form's
    /// request_key is unique (interfaces.json ruleEnforcement: domain · db).
    /// </summary>
    public partial class AddStockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    counted_qty = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_key = table.Column<string>(type: "text", nullable: false),
                    adjusted_by_id = table.Column<int>(type: "integer", nullable: false),
                    adjusted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustments", x => x.id);
                    table.CheckConstraint("ck_stock_adjustments_counted_qty", "counted_qty IS NULL OR counted_qty >= 0");
                    table.CheckConstraint("ck_stock_adjustments_quantity_delta", "quantity_delta <> 0");
                    table.ForeignKey(
                        name: "FK_stock_adjustments_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_adjustments_user_accounts_adjusted_by_id",
                        column: x => x.adjusted_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_adjustments_adjusted_by_id",
                table: "stock_adjustments",
                column: "adjusted_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_product_adjusted_at",
                table: "stock_adjustments",
                columns: new[] { "product_id", "adjusted_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_request_key",
                table: "stock_adjustments",
                column: "request_key",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION stock_adjustments_insert_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'stock_adjustments is insert-only (BR-talad-032): % refused', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER stock_adjustments_insert_only
                    BEFORE UPDATE OR DELETE ON stock_adjustments
                    FOR EACH ROW EXECUTE FUNCTION stock_adjustments_insert_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER stock_adjustments_insert_only ON stock_adjustments;
                DROP FUNCTION stock_adjustments_insert_only();
                """);

            migrationBuilder.DropTable(
                name: "stock_adjustments");
        }
    }
}
