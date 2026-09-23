using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    accumulated_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hidden_by_id = table.Column<int>(type: "integer", nullable: true),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.id);
                    table.CheckConstraint("ck_members_accumulated_amount", "accumulated_amount >= 0");
                    table.ForeignKey(
                        name: "FK_members_user_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_members_user_accounts_hidden_by_id",
                        column: x => x.hidden_by_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_members_created_by_id",
                table: "members",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_members_hidden_by_id",
                table: "members",
                column: "hidden_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_members_phone_active",
                table: "members",
                column: "phone",
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "members");
        }
    }
}
