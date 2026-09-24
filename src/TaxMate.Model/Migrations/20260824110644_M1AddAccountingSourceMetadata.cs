using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M1AddAccountingSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Transactions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceReviewedAt",
                table: "TaxPeriods",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceReviewedByUserId",
                table: "TaxPeriods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseId",
                table: "IngredientPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingType",
                table: "Incomes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "Incomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNumber",
                table: "Expenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S2cGroupCode",
                table: "ExpenseCategories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Transactions" AS t
                SET "CompletedAt" = paid."CompletedAt"
                FROM
                (
                    SELECT "TransactionId", MAX("PaidAt") AS "CompletedAt"
                    FROM "Payments"
                    WHERE "PaidAt" IS NOT NULL
                    GROUP BY "TransactionId"
                ) paid
                WHERE t."TransactionId" = paid."TransactionId"
                  AND t."Status" = 'Completed'
                  AND t."CompletedAt" IS NULL;

                UPDATE "Expenses"
                SET "VoucherNumber" =
                    'PC-LEGACY-' || "ExpenseId"::text
                WHERE "VoucherNumber" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "VoucherNumber",
                table: "Expenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BusinessId_CompletedAt",
                table: "Transactions",
                columns: new[] { "BusinessId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_EvidenceReviewedByUserId",
                table: "TaxPeriods",
                column: "EvidenceReviewedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPeriods_EvidenceReviewPair",
                table: "TaxPeriods",
                sql: "(\"EvidenceReviewedAt\" IS NULL AND \"EvidenceReviewedByUserId\" IS NULL) OR (\"EvidenceReviewedAt\" IS NOT NULL AND \"EvidenceReviewedByUserId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_ExpenseId",
                table: "IngredientPurchases",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_TransactionId",
                table: "Incomes",
                column: "TransactionId",
                unique: true,
                filter: "\"TransactionId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Incomes_AccountingType",
                table: "Incomes",
                sql: "\"AccountingType\" IS NULL OR \"AccountingType\" IN ('BusinessRevenue', 'NonRevenueCashIn')");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BusinessId_VoucherNumber",
                table: "Expenses",
                columns: new[] { "BusinessId", "VoucherNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExpenseCategories_S2cGroupCode",
                table: "ExpenseCategories",
                sql: "\"S2cGroupCode\" IS NULL OR \"S2cGroupCode\" IN ('Labor', 'PurchasedServices', 'OtherDirect')");

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_Transactions_TransactionId",
                table: "Incomes",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientPurchases_Expenses_ExpenseId",
                table: "IngredientPurchases",
                column: "ExpenseId",
                principalTable: "Expenses",
                principalColumn: "ExpenseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaxPeriods_Users_EvidenceReviewedByUserId",
                table: "TaxPeriods",
                column: "EvidenceReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_Transactions_TransactionId",
                table: "Incomes");

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientPurchases_Expenses_ExpenseId",
                table: "IngredientPurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_TaxPeriods_Users_EvidenceReviewedByUserId",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BusinessId_CompletedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TaxPeriods_EvidenceReviewedByUserId",
                table: "TaxPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPeriods_EvidenceReviewPair",
                table: "TaxPeriods");

            migrationBuilder.DropIndex(
                name: "IX_IngredientPurchases_ExpenseId",
                table: "IngredientPurchases");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_TransactionId",
                table: "Incomes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Incomes_AccountingType",
                table: "Incomes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BusinessId_VoucherNumber",
                table: "Expenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExpenseCategories_S2cGroupCode",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "EvidenceReviewedAt",
                table: "TaxPeriods");

            migrationBuilder.DropColumn(
                name: "EvidenceReviewedByUserId",
                table: "TaxPeriods");

            migrationBuilder.DropColumn(
                name: "ExpenseId",
                table: "IngredientPurchases");

            migrationBuilder.DropColumn(
                name: "AccountingType",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "VoucherNumber",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "S2cGroupCode",
                table: "ExpenseCategories");
        }
    }
}
