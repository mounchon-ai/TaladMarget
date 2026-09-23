using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "member_id",
                table: "carts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_carts_member_id",
                table: "carts",
                column: "member_id");

            migrationBuilder.AddForeignKey(
                name: "FK_carts_members_member_id",
                table: "carts",
                column: "member_id",
                principalTable: "members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_carts_members_member_id",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "IX_carts_member_id",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "member_id",
                table: "carts");
        }
    }
}
