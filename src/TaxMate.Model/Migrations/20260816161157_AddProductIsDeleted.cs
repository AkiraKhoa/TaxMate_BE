using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: local DBs may already have these columns from earlier sync gaps.
            migrationBuilder.Sql("""
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "CostPrice" numeric(18,6) NULL;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "StockQuantity" numeric(18,4) NULL;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Ingredients" ADD COLUMN IF NOT EXISTS "StockQuantity" numeric(18,4) NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");
        }
    }
}
