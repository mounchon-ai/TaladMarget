using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BR-talad-033@v1 at db (interfaces.json ruleEnforcement: domain · db) — a price version is written once:
    /// every change of price is a new row, so the database refuses an UPDATE or a DELETE of one.
    /// </summary>
    public partial class GuardPriceVersionsInsertOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION product_price_versions_insert_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'product_price_versions is insert-only (BR-talad-033): % refused', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER product_price_versions_insert_only
                    BEFORE UPDATE OR DELETE ON product_price_versions
                    FOR EACH ROW EXECUTE FUNCTION product_price_versions_insert_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER product_price_versions_insert_only ON product_price_versions;
                DROP FUNCTION product_price_versions_insert_only();
                """);
        }
    }
}
