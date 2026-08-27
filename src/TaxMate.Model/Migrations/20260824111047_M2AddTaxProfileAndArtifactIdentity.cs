using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M2AddTaxProfileAndArtifactIdentity : Migration
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
                        FROM "TaxCalculations"
                        WHERE "IsCurrent" = TRUE
                        GROUP BY "TaxPeriodId", "RecommendedFormCode"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Multiple current calculations exist for the same period/form; review before M2.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM "TaxDeclarations"
                        WHERE "IsCurrent" = TRUE
                        GROUP BY "TaxPeriodId", "FormCode"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Multiple current declarations exist for the same period/form; review before M2.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_IsCurrent",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_Version",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxCalculations_TaxPeriodId_IsCurrent",
                table: "TaxCalculations");

            migrationBuilder.DropIndex(
                name: "IX_TaxCalculations_TaxPeriodId_Version",
                table: "TaxCalculations");

            migrationBuilder.DropIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Year",
                table: "RevenueThresholdAlerts");

            migrationBuilder.AddColumn<string>(
                name: "CommencementPeriod",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommencementTaxYear",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclaredRevenueBracket",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalIncomeTaxMethod",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMethodEffectiveYear",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxProfileConfirmedAt",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxType",
                table: "TaxPayments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormDataJson",
                table: "TaxDeclarations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxMethod",
                table: "TaxCalculations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMethodEffectiveYear",
                table: "TaxCalculations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "RevenueThresholdAlerts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RevenueThresholdAlerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ThresholdAmount",
                table: "RevenueThresholdAlerts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThresholdCode",
                table: "RevenueThresholdAlerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "TaxPayments"
                SET "TaxType" = 'Unknown'
                WHERE "TaxType" IS NULL;

                UPDATE "TaxCalculations"
                SET "TaxMethod" = 'RevenueBased'
                WHERE "TaxMethod" IS NULL;

                UPDATE "RevenueThresholdAlerts" alert
                SET
                    "ThresholdCode" = 'Crossed1B',
                    "ThresholdAmount" = COALESCE
                    (
                        (
                            SELECT setting."Amount"
                            FROM "TaxThresholdSettings" setting
                            WHERE setting."Type" = 'AnnualRevenueTax'
                              AND setting."EffectiveFrom" <= make_date(alert."Year", 12, 31)
                            ORDER BY setting."EffectiveFrom" DESC
                            LIMIT 1
                        ),
                        1000000000
                    ),
                    "Status" = 'PendingReview',
                    "ResolvedAt" = NULL
                WHERE "ThresholdCode" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TaxType",
                table: "TaxPayments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TaxMethod",
                table: "TaxCalculations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RevenueThresholdAlerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThresholdAmount",
                table: "RevenueThresholdAlerts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ThresholdCode",
                table: "RevenueThresholdAlerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "TaxThresholdSettings",
                columns: new[] { "Id", "Amount", "CreatedAt", "EffectiveFrom", "Type", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("20260000-0000-4000-a000-000000000013"), 3000000000m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), "IncomeBasedRequirement", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20260000-0000-4000-a000-000000000014"), 50000000000m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 1), "SupportedRevenueCeiling", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_CommencementPair",
                table: "Users",
                sql: "(\"CommencementPeriod\" IS NULL AND \"CommencementTaxYear\" IS NULL) OR (\"CommencementPeriod\" IS NOT NULL AND \"CommencementTaxYear\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_CommencementPeriod",
                table: "Users",
                sql: "\"CommencementPeriod\" IS NULL OR \"CommencementPeriod\" IN ('BeforeTaxYear', 'FirstHalfOfTaxYear', 'SecondHalfOfTaxYear')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_DeclaredRevenueBracket",
                table: "Users",
                sql: "\"DeclaredRevenueBracket\" IS NULL OR \"DeclaredRevenueBracket\" IN ('AtOrBelow1B', 'Over1BTo3B', 'Over3BTo50B')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_PersonalIncomeTaxMethod",
                table: "Users",
                sql: "\"PersonalIncomeTaxMethod\" IS NULL OR \"PersonalIncomeTaxMethod\" IN ('RevenueBased', 'IncomeBased')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_TaxMethodPair",
                table: "Users",
                sql: "(\"PersonalIncomeTaxMethod\" IS NULL AND \"TaxMethodEffectiveYear\" IS NULL) OR (\"PersonalIncomeTaxMethod\" IS NOT NULL AND \"TaxMethodEffectiveYear\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_TaxProfileCompatibility",
                table: "Users",
                sql: "(\"DeclaredRevenueBracket\" IS NULL AND \"PersonalIncomeTaxMethod\" IS NULL AND \"TaxMethodEffectiveYear\" IS NULL AND \"CommencementPeriod\" IS NULL AND \"CommencementTaxYear\" IS NULL AND \"TaxProfileConfirmedAt\" IS NULL) OR (\"DeclaredRevenueBracket\" = 'AtOrBelow1B' AND \"PersonalIncomeTaxMethod\" IS NULL) OR (\"DeclaredRevenueBracket\" = 'Over1BTo3B' AND \"PersonalIncomeTaxMethod\" IN ('RevenueBased', 'IncomeBased') AND \"CommencementPeriod\" IS NULL) OR (\"DeclaredRevenueBracket\" = 'Over3BTo50B' AND \"PersonalIncomeTaxMethod\" = 'IncomeBased' AND \"CommencementPeriod\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_TaxType_Status_PaymentDate",
                table: "TaxPayments",
                columns: new[] { "TaxType", "Status", "PaymentDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPayments_TaxType",
                table: "TaxPayments",
                sql: "\"TaxType\" IN ('VAT', 'PIT', 'Unknown')");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_FormCode",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "FormCode" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_FormCode_Version",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "FormCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "RecommendedFormCode" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode_Version",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "RecommendedFormCode", "Version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations",
                sql: "\"TaxMethod\" IN ('RevenueBased', 'IncomeBased')");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Status",
                table: "RevenueThresholdAlerts",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Year_ThresholdCode",
                table: "RevenueThresholdAlerts",
                columns: new[] { "OwnerId", "Year", "ThresholdCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Amount",
                table: "RevenueThresholdAlerts",
                sql: "\"ThresholdAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Code",
                table: "RevenueThresholdAlerts",
                sql: "\"ThresholdCode\" IN ('Crossed1B', 'Crossed3B', 'Crossed50B')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Resolution",
                table: "RevenueThresholdAlerts",
                sql: "(\"Status\" = 'Resolved' AND \"ResolvedAt\" IS NOT NULL) OR (\"Status\" <> 'Resolved' AND \"ResolvedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Status",
                table: "RevenueThresholdAlerts",
                sql: "\"Status\" IN ('PendingReview', 'Acknowledged', 'Resolved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_CommencementPair",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_CommencementPeriod",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_DeclaredRevenueBracket",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_PersonalIncomeTaxMethod",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_TaxMethodPair",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_TaxProfileCompatibility",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TaxPayments_TaxType_Status_PaymentDate",
                table: "TaxPayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPayments_TaxType",
                table: "TaxPayments");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_FormCode",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_FormCode_Version",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode",
                table: "TaxCalculations");

            migrationBuilder.DropIndex(
                name: "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode_Version",
                table: "TaxCalculations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxCalculations_TaxMethod",
                table: "TaxCalculations");

            migrationBuilder.DropIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Status",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Year_ThresholdCode",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Amount",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Code",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Resolution",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueThresholdAlerts_Status",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DeleteData(
                table: "TaxThresholdSettings",
                keyColumn: "Id",
                keyValue: new Guid("20260000-0000-4000-a000-000000000013"));

            migrationBuilder.DeleteData(
                table: "TaxThresholdSettings",
                keyColumn: "Id",
                keyValue: new Guid("20260000-0000-4000-a000-000000000014"));

            migrationBuilder.DropColumn(
                name: "CommencementPeriod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CommencementTaxYear",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeclaredRevenueBracket",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PersonalIncomeTaxMethod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxMethodEffectiveYear",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxProfileConfirmedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxType",
                table: "TaxPayments");

            migrationBuilder.DropColumn(
                name: "FormDataJson",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "TaxMethod",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "TaxMethodEffectiveYear",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropColumn(
                name: "ThresholdAmount",
                table: "RevenueThresholdAlerts");

            migrationBuilder.DropColumn(
                name: "ThresholdCode",
                table: "RevenueThresholdAlerts");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_IsCurrent",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_Version",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_IsCurrent",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_Version",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevenueThresholdAlerts_OwnerId_Year",
                table: "RevenueThresholdAlerts",
                columns: new[] { "OwnerId", "Year" },
                unique: true);
        }
    }
}
