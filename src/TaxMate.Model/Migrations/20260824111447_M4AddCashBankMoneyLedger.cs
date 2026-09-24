using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M4AddCashBankMoneyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAccounts_BusinessProfiles_BusinessId",
                table: "PaymentAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentAccounts_PaymentAccountId",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "BankShortName",
                table: "PaymentAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "PaymentAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "PaymentAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "PaymentAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "PaymentAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialBalance",
                table: "PaymentAccounts",
                type: "numeric(20,2)",
                precision: 20,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InitialBalanceDate",
                table: "PaymentAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PaymentAccounts",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "PaymentAccounts"
                SET
                    "AccountType" = 'Bank',
                    "IsActive" = TRUE
                WHERE "AccountType" IS NULL;

                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "PaymentAccounts"
                        WHERE "AccountType" = 'Bank'
                          AND "IsDefault" = TRUE
                          AND "IsActive" = TRUE
                        GROUP BY "BusinessId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Multiple active default bank accounts exist for a business; review before M4.';
                    END IF;
                END $$;

                INSERT INTO "PaymentAccounts"
                (
                    "PaymentAccountId", "BusinessId", "IsDefault",
                    "Description", "AccountType", "IsActive",
                    "CreatedAt", "UpdatedAt"
                )
                SELECT
                    gen_random_uuid(),
                    business."Id",
                    FALSE,
                    'Tiền mặt',
                    'Cash',
                    TRUE,
                    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
                FROM "BusinessProfiles" business
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM "PaymentAccounts" account
                    WHERE account."BusinessId" = business."Id"
                      AND account."AccountType" = 'Cash'
                );

                UPDATE "Payments" payment
                SET "PaymentAccountId" = cash."PaymentAccountId"
                FROM "Transactions" AS t, "PaymentAccounts" AS cash
                WHERE payment."TransactionId" = t."TransactionId"
                  AND cash."BusinessId" = t."BusinessId"
                  AND cash."AccountType" = 'Cash'
                  AND payment."PaymentAccountId" IS NULL
                  AND LOWER(BTRIM(payment."PaymentMethod")) = 'cash';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AccountType",
                table: "PaymentAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "PaymentAccounts",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "MoneyMovements",
                columns: table => new
                {
                    MoneyMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyMovements", x => x.MoneyMovementId);
                    table.CheckConstraint("CK_MoneyMovements_AmountPositive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_MoneyMovements_Type", "\"MovementType\" IN ('PaymentIn', 'ManualIncomeIn', 'ExpenseOut')");
                    table.ForeignKey(
                        name: "FK_MoneyMovements_PaymentAccounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "PaymentAccounts",
                        principalColumn: "PaymentAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_BusinessId_AccountType_IsActive",
                table: "PaymentAccounts",
                columns: new[] { "BusinessId", "AccountType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_OneActiveDefaultBank",
                table: "PaymentAccounts",
                column: "BusinessId",
                unique: true,
                filter: "\"AccountType\" = 'Bank' AND \"IsDefault\" = TRUE AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_OneCashAccount",
                table: "PaymentAccounts",
                column: "BusinessId",
                unique: true,
                filter: "\"AccountType\" = 'Cash'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAccounts_AccountType",
                table: "PaymentAccounts",
                sql: "\"AccountType\" IN ('Cash', 'Bank')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAccounts_BankFields",
                table: "PaymentAccounts",
                sql: "\"AccountType\" <> 'Bank' OR (\"BankShortName\" IS NOT NULL AND \"BankName\" IS NOT NULL AND \"AccountNumber\" IS NOT NULL AND \"AccountName\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAccounts_InitialBalancePair",
                table: "PaymentAccounts",
                sql: "(\"InitialBalance\" IS NULL AND \"InitialBalanceDate\" IS NULL) OR (\"InitialBalance\" IS NOT NULL AND \"InitialBalanceDate\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyMovements_MovementType_ReferenceId",
                table: "MoneyMovements",
                columns: new[] { "MovementType", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyMovements_PaymentAccountId_MovementDate",
                table: "MoneyMovements",
                columns: new[] { "PaymentAccountId", "MovementDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAccounts_BusinessProfiles_BusinessId",
                table: "PaymentAccounts",
                column: "BusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentAccounts_PaymentAccountId",
                table: "Payments",
                column: "PaymentAccountId",
                principalTable: "PaymentAccounts",
                principalColumn: "PaymentAccountId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAccounts_BusinessProfiles_BusinessId",
                table: "PaymentAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentAccounts_PaymentAccountId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "MoneyMovements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAccounts_BusinessId_AccountType_IsActive",
                table: "PaymentAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAccounts_OneActiveDefaultBank",
                table: "PaymentAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAccounts_OneCashAccount",
                table: "PaymentAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAccounts_AccountType",
                table: "PaymentAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAccounts_BankFields",
                table: "PaymentAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAccounts_InitialBalancePair",
                table: "PaymentAccounts");

            migrationBuilder.Sql("""
                UPDATE "Payments" payment
                SET "PaymentAccountId" = NULL
                FROM "PaymentAccounts" account
                WHERE payment."PaymentAccountId" = account."PaymentAccountId"
                  AND account."AccountType" = 'Cash';

                DELETE FROM "PaymentAccounts"
                WHERE "AccountType" = 'Cash';
                """);

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "InitialBalance",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "InitialBalanceDate",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PaymentAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "BankShortName",
                table: "PaymentAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "PaymentAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "PaymentAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "PaymentAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAccounts_BusinessProfiles_BusinessId",
                table: "PaymentAccounts",
                column: "BusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentAccounts_PaymentAccountId",
                table: "Payments",
                column: "PaymentAccountId",
                principalTable: "PaymentAccounts",
                principalColumn: "PaymentAccountId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
