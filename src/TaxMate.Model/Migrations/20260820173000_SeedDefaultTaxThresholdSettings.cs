using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaxMate.Model.Data;

#nullable disable

namespace TaxMate.Model.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820173000_SeedDefaultTaxThresholdSettings")]
    public partial class SeedDefaultTaxThresholdSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TaxThresholdSettings"
                SET "Id" = '20260000-0000-4000-a000-000000000011'
                WHERE "Type" = 'AnnualRevenueTax'
                  AND "EffectiveFrom" = DATE '2026-01-01'
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM "TaxThresholdSettings"
                      WHERE "Id" =
                          '20260000-0000-4000-a000-000000000011'
                  );

                UPDATE "TaxThresholdSettings"
                SET "Id" = '20260000-0000-4000-a000-000000000012'
                WHERE "Type" = 'EInvoiceRequirement'
                  AND "EffectiveFrom" = DATE '2026-01-01'
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM "TaxThresholdSettings"
                      WHERE "Id" =
                          '20260000-0000-4000-a000-000000000012'
                  );

                INSERT INTO "TaxThresholdSettings"
                (
                    "Id", "Type", "Amount", "EffectiveFrom",
                    "UpdatedByUserId", "CreatedAt", "UpdatedAt"
                )
                VALUES
                (
                    '20260000-0000-4000-a000-000000000011',
                    'AnnualRevenueTax',
                    1000000000,
                    DATE '2026-01-01',
                    NULL,
                    TIMESTAMP '2026-01-01 00:00:00',
                    TIMESTAMP '2026-01-01 00:00:00'
                ),
                (
                    '20260000-0000-4000-a000-000000000012',
                    'EInvoiceRequirement',
                    1000000000,
                    DATE '2026-01-01',
                    NULL,
                    TIMESTAMP '2026-01-01 00:00:00',
                    TIMESTAMP '2026-01-01 00:00:00'
                )
                ON CONFLICT ("Type", "EffectiveFrom") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxThresholdSettings",
                keyColumn: "Id",
                keyValue: new Guid("20260000-0000-4000-a000-000000000011"));

            migrationBuilder.DeleteData(
                table: "TaxThresholdSettings",
                keyColumn: "Id",
                keyValue: new Guid("20260000-0000-4000-a000-000000000012"));
        }
    }
}
