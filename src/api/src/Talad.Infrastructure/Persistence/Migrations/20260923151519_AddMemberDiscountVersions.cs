using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberDiscountVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_discount_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    rate_percent = table.Column<int>(type: "integer", nullable: false),
                    changed_by_id = table.Column<int>(type: "integer", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_discount_versions", x => x.id);
                    table.CheckConstraint("ck_member_discount_versions_rate", "rate_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_member_discount_versions_user_accounts_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_discount_versions_changed_by_id",
                table: "member_discount_versions",
                column: "changed_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_discount_versions");
        }
    }
}
