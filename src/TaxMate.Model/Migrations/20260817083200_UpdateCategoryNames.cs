using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCategoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both columns are already part of InitialMigrate in the current
            // baseline. IF NOT EXISTS keeps this historical migration safe for
            // older databases created from an earlier baseline that lacked them.
            migrationBuilder.Sql("""
                ALTER TABLE "Products"
                    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE;

                ALTER TABLE "BusinessProfiles"
                    ADD COLUMN IF NOT EXISTS "IsStockTrackingEnabled" boolean NOT NULL DEFAULT FALSE;
                """);

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000003"),
                column: "Name",
                value: "Dịch vụ");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "FNB");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not drop these columns: the current InitialMigrate owns them.
            // A rollback to that baseline must preserve its schema.

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000003"),
                column: "Name",
                value: "Dịch vụ");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "F&B");
        }
    }
}
