using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TaxMate.Model.Data;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260826090000_Shot1AddTknFilingPeriods")]
    public partial class Shot1AddTknFilingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilingWindow",
                table: "TaxPeriods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods",
                sql: "(\"PeriodType\" = 'Monthly' AND \"Month\" BETWEEN 1 AND 12 AND \"Quarter\" IS NULL AND \"FilingWindow\" IS NULL) OR " +
                     "(\"PeriodType\" = 'Quarterly' AND \"Month\" IS NULL AND \"Quarter\" BETWEEN 1 AND 4 AND \"FilingWindow\" IS NULL) OR " +
                     "(\"PeriodType\" = 'Yearly' AND \"Month\" IS NULL AND \"Quarter\" IS NULL AND \"FilingWindow\" IS NULL) OR " +
                     "(\"PeriodType\" = 'Tkn' AND \"Month\" IS NULL AND \"Quarter\" IS NULL AND \"FilingWindow\" IN ('FirstHalf', 'SecondHalf', 'Annual'))");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year_FilingWindow",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year", "FilingWindow" },
                unique: true,
                filter: "\"PeriodType\" = 'Tkn'");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations",
                sql: "\"TaxMethod\" IN ('RevenueBased', 'IncomeBased', 'NotApplicable')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxCalculations"
                        WHERE "TaxMethod" = 'NotApplicable'
                    ) THEN
                        RAISE EXCEPTION
                            'TaxCalculations contains NotApplicable snapshots; Shot1 rollback refused.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations",
                sql: "\"TaxMethod\" IN ('RevenueBased', 'IncomeBased')");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_BusinessId_Year_FilingWindow",
                table: "TaxPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxPeriods"
                        WHERE "PeriodType" = 'Tkn'
                           OR "FilingWindow" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'TaxPeriods contains TKN filing periods; Shot1 rollback refused.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPeriods_PeriodShape",
                table: "TaxPeriods",
                sql: "(\"PeriodType\" = 'Monthly' AND \"Month\" BETWEEN 1 AND 12 AND \"Quarter\" IS NULL) OR " +
                     "(\"PeriodType\" = 'Quarterly' AND \"Month\" IS NULL AND \"Quarter\" BETWEEN 1 AND 4) OR " +
                     "(\"PeriodType\" = 'Yearly' AND \"Month\" IS NULL AND \"Quarter\" IS NULL)");

            migrationBuilder.DropColumn(
                name: "FilingWindow",
                table: "TaxPeriods");
        }
    }
}
