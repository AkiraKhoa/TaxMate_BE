using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M7NormalizeTaxPeriodBangkokBoundaries : Migration
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
                        WHERE "Year" NOT BETWEEN 2 AND 9998
                           OR NOT
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
                            'TaxPeriods contains an unsupported identity shape or year outside 2..9998; review before M7.';
                    END IF;

                    UPDATE "TaxPeriods"
                    SET
                        "PeriodStartDate" =
                            CASE "PeriodType"
                                WHEN 'Monthly' THEN
                                    make_date("Year", "Month", 1)::timestamp
                                        - INTERVAL '7 hours'
                                WHEN 'Quarterly' THEN
                                    make_date(
                                        "Year",
                                        (("Quarter" - 1) * 3) + 1,
                                        1)::timestamp
                                        - INTERVAL '7 hours'
                                WHEN 'Yearly' THEN
                                    make_date("Year", 1, 1)::timestamp
                                        - INTERVAL '7 hours'
                            END,
                        "PeriodEndDate" =
                            CASE "PeriodType"
                                WHEN 'Monthly' THEN
                                    make_date("Year", "Month", 1)::timestamp
                                        + INTERVAL '1 month'
                                        - INTERVAL '7 hours'
                                WHEN 'Quarterly' THEN
                                    make_date(
                                        "Year",
                                        (("Quarter" - 1) * 3) + 1,
                                        1)::timestamp
                                        + INTERVAL '3 months'
                                        - INTERVAL '7 hours'
                                WHEN 'Yearly' THEN
                                    make_date("Year", 1, 1)::timestamp
                                        + INTERVAL '1 year'
                                        - INTERVAL '7 hours'
                            END;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Arbitrary pre-M7 timestamps cannot be reconstructed. The identity
            // fields do deterministically define the former seed convention, so
            // Down restores UTC-midnight starts and inclusive 23:59:59 ends.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxPeriods"
                        WHERE "Year" NOT BETWEEN 2 AND 9998
                           OR NOT
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
                            'TaxPeriods contains an unsupported identity shape or year outside 2..9998; M7 rollback refused.';
                    END IF;

                    UPDATE "TaxPeriods"
                    SET
                        "PeriodStartDate" =
                            CASE "PeriodType"
                                WHEN 'Monthly' THEN
                                    make_date("Year", "Month", 1)::timestamp
                                WHEN 'Quarterly' THEN
                                    make_date(
                                        "Year",
                                        (("Quarter" - 1) * 3) + 1,
                                        1)::timestamp
                                WHEN 'Yearly' THEN
                                    make_date("Year", 1, 1)::timestamp
                            END,
                        "PeriodEndDate" =
                            CASE "PeriodType"
                                WHEN 'Monthly' THEN
                                    make_date("Year", "Month", 1)::timestamp
                                        + INTERVAL '1 month'
                                        - INTERVAL '1 second'
                                WHEN 'Quarterly' THEN
                                    make_date(
                                        "Year",
                                        (("Quarter" - 1) * 3) + 1,
                                        1)::timestamp
                                        + INTERVAL '3 months'
                                        - INTERVAL '1 second'
                                WHEN 'Yearly' THEN
                                    make_date("Year", 1, 1)::timestamp
                                        + INTERVAL '1 year'
                                        - INTERVAL '1 second'
                            END;
                END $$;
                """);
        }
    }
}
