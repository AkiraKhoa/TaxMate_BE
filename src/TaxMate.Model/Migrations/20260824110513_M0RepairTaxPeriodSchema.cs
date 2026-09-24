using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M0RepairTaxPeriodSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxPeriods"
                        WHERE "BusinessProfileId" IS NOT NULL
                          AND "BusinessProfileId" <> "BusinessId"
                    ) THEN
                        RAISE EXCEPTION
                            'TaxPeriods contains mismatched BusinessId/BusinessProfileId values; review before M0.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxPeriods"
                        GROUP BY "BusinessId", "Year",
                            CASE WHEN "PeriodType" = 'Monthly' THEN "Month" END,
                            CASE WHEN "PeriodType" = 'Quarterly' THEN "Quarter" END,
                            "PeriodType"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'TaxPeriods contains duplicate period identities; review before M0.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxPeriods"
                        WHERE NOT
                        (
                            ("PeriodType" = 'Monthly'
                                AND "Month" BETWEEN 1 AND 12
                                AND "Quarter" IS NULL)
                            OR ("PeriodType" = 'Quarterly'
                                AND "Month" IS NULL
                                AND "Quarter" BETWEEN 1 AND 4)
                            OR ("PeriodType" = 'Yearly'
                                AND "Month" IS NULL
                                AND "Quarter" IS NULL)
                        )
                    ) THEN
                        RAISE EXCEPTION
                            'TaxPeriods contains invalid period shapes; review before M0.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TaxPeriods_BusinessProfiles_BusinessProfileId",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_PeriodType_Year_Month_Quarter",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Month_Quarter",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessProfileId",
                table: "TaxPeriods");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "TaxPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year" },
                unique: true,
                filter: "\"PeriodType\" = 'Yearly'");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Month",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year", "Month" },
                unique: true,
                filter: "\"PeriodType\" = 'Monthly'");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Quarter",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year", "Quarter" },
                unique: true,
                filter: "\"PeriodType\" = 'Quarterly'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods",
                sql: "(\"PeriodType\" = 'Monthly' AND \"Month\" BETWEEN 1 AND 12 AND \"Quarter\" IS NULL) OR (\"PeriodType\" = 'Quarterly' AND \"Month\" IS NULL AND \"Quarter\" BETWEEN 1 AND 4) OR (\"PeriodType\" = 'Yearly' AND \"Month\" IS NULL AND \"Quarter\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_Year",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Month",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Quarter",
                table: "TaxPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "TaxPeriods",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_PeriodType_Year_Month_Quarter",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "PeriodType", "Year", "Month", "Quarter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Month_Quarter",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year", "Month", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessProfileId",
                table: "TaxPeriods",
                column: "BusinessProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaxPeriods_BusinessProfiles_BusinessProfileId",
                table: "TaxPeriods",
                column: "BusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id");
        }
    }
}
