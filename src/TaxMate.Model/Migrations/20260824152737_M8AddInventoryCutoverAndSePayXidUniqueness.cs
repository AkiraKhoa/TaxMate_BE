using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M8AddInventoryCutoverAndSePayXidUniqueness : Migration
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
                        FROM "PaymentAccounts"
                        WHERE "SePayBankAccountXid" IS NOT NULL
                        GROUP BY "SePayBankAccountXid"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'PaymentAccounts contains duplicate non-null SePayBankAccountXid values; resolve them before M8.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "InventoryInitializedAt",
                table: "BusinessProfiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_SePayBankAccountXid",
                table: "PaymentAccounts",
                column: "SePayBankAccountXid",
                unique: true,
                filter: "\"SePayBankAccountXid\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentAccounts_SePayBankAccountXid",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "InventoryInitializedAt",
                table: "BusinessProfiles");
        }
    }
}
