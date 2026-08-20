using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaxMate.Model.Data;

#nullable disable

namespace TaxMate.Model.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820170000_UseEffectiveDatedTaxThresholds")]
    public partial class UseEffectiveDatedTaxThresholds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxThresholdSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false),
                    Amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(
                        type: "date",
                        nullable: false),
                    UpdatedByUserId = table.Column<Guid>(
                        type: "uuid",
                        nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxThresholdSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxThresholdSettings_Type_EffectiveFrom",
                table: "TaxThresholdSettings",
                columns: new[] { "Type", "EffectiveFrom" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "TaxThresholdSettings"
                (
                    "Id", "Type", "Amount", "EffectiveFrom",
                    "UpdatedByUserId", "CreatedAt", "UpdatedAt"
                )
                SELECT
                    gen_random_uuid(),
                    'AnnualRevenueTax',
                    "AnnualRevenueThreshold",
                    make_date("Year", 1, 1),
                    "UpdatedByUserId",
                    "CreatedAt",
                    "UpdatedAt"
                FROM "TaxPolicySettings"
                UNION ALL
                SELECT
                    gen_random_uuid(),
                    'EInvoiceRequirement',
                    "EInvoiceRevenueThreshold",
                    make_date("Year", 1, 1),
                    "UpdatedByUserId",
                    "CreatedAt",
                    "UpdatedAt"
                FROM "TaxPolicySettings";
                """);

            migrationBuilder.DropTable(name: "TaxPolicySettings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxPolicySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    AnnualRevenueThreshold = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),
                    EInvoiceRevenueThreshold = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),
                    UpdatedByUserId = table.Column<Guid>(
                        type: "uuid",
                        nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPolicySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicySettings_Year",
                table: "TaxPolicySettings",
                column: "Year",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "TaxPolicySettings"
                (
                    "Id", "Year", "AnnualRevenueThreshold",
                    "EInvoiceRevenueThreshold", "UpdatedByUserId",
                    "CreatedAt", "UpdatedAt"
                )
                SELECT
                    gen_random_uuid(),
                    years."Year",
                    COALESCE(
                        (
                            SELECT setting."Amount"
                            FROM "TaxThresholdSettings" setting
                            WHERE setting."Type" = 'AnnualRevenueTax'
                              AND EXTRACT(YEAR FROM setting."EffectiveFrom")::integer =
                                  years."Year"
                            ORDER BY setting."EffectiveFrom" DESC
                            LIMIT 1
                        ),
                        1000000000
                    ),
                    COALESCE(
                        (
                            SELECT setting."Amount"
                            FROM "TaxThresholdSettings" setting
                            WHERE setting."Type" = 'EInvoiceRequirement'
                              AND EXTRACT(YEAR FROM setting."EffectiveFrom")::integer =
                                  years."Year"
                            ORDER BY setting."EffectiveFrom" DESC
                            LIMIT 1
                        ),
                        1000000000
                    ),
                    NULL,
                    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
                FROM
                (
                    SELECT DISTINCT
                        EXTRACT(YEAR FROM "EffectiveFrom")::integer AS "Year"
                    FROM "TaxThresholdSettings"
                ) years;
                """);

            migrationBuilder.DropTable(name: "TaxThresholdSettings");
        }
    }
}
