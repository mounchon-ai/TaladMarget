using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotion_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    promotion_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    product_a_id = table.Column<int>(type: "integer", nullable: true),
                    qty_a = table.Column<int>(type: "integer", nullable: true),
                    product_b_id = table.Column<int>(type: "integer", nullable: true),
                    qty_b = table.Column<int>(type: "integer", nullable: true),
                    free_product_id = table.Column<int>(type: "integer", nullable: true),
                    free_qty = table.Column<int>(type: "integer", nullable: true),
                    rate_percent = table.Column<int>(type: "integer", nullable: true),
                    min_subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_versions", x => x.id);
                    table.CheckConstraint("ck_promotion_versions_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_promotion_versions_free_qty", "free_qty IS NULL OR free_qty >= 1");
                    table.CheckConstraint("ck_promotion_versions_min_subtotal", "min_subtotal IS NULL OR min_subtotal >= 0");
                    table.CheckConstraint("ck_promotion_versions_qty_a", "qty_a IS NULL OR qty_a >= 1");
                    table.CheckConstraint("ck_promotion_versions_qty_b", "qty_b IS NULL OR qty_b >= 1");
                    table.CheckConstraint("ck_promotion_versions_rate", "rate_percent IS NULL OR rate_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_promotion_versions_products_free_product_id",
                        column: x => x.free_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_versions_products_product_a_id",
                        column: x => x.product_a_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_versions_products_product_b_id",
                        column: x => x.product_b_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_versions_user_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    current_version_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotions", x => x.id);
                    table.ForeignKey(
                        name: "FK_promotions_promotion_versions_current_version_id",
                        column: x => x.current_version_id,
                        principalTable: "promotion_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_versions_created_by_id",
                table: "promotion_versions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_versions_free_product_id",
                table: "promotion_versions",
                column: "free_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_versions_product_a_id",
                table: "promotion_versions",
                column: "product_a_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_versions_product_b_id",
                table: "promotion_versions",
                column: "product_b_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_versions_promotion_id",
                table: "promotion_versions",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_current_version_id",
                table: "promotions",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_status",
                table: "promotions",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_promotion_versions_promotions_promotion_id",
                table: "promotion_versions",
                column: "promotion_id",
                principalTable: "promotions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_promotion_versions_promotions_promotion_id",
                table: "promotion_versions");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropTable(
                name: "promotion_versions");
        }
    }
}
