using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M3AddInventoryLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1 FROM "Products"
                        WHERE ABS("StockQuantity") >= 1000000000000
                    ) OR EXISTS
                    (
                        SELECT 1 FROM "Ingredients"
                        WHERE ABS("StockQuantity") >= 1000000000000
                    ) THEN
                        RAISE EXCEPTION
                            'StockQuantity exceeds numeric(18,6) range; review before M3.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "StockQuantity",
                table: "Products",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StockQuantity",
                table: "Ingredients",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    InventoryMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.InventoryMovementId);
                    table.CheckConstraint("CK_InventoryMovements_ExactlyOneItem", "(\"ProductId\" IS NOT NULL AND \"IngredientId\" IS NULL) OR (\"ProductId\" IS NULL AND \"IngredientId\" IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryMovements_QuantityPositive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_InventoryMovements_ReferenceShape", "(\"MovementType\" IN ('PurchaseIn', 'OrderOut') AND \"ReferenceId\" IS NOT NULL) OR (\"MovementType\" IN ('OpeningBalance', 'AdjustmentIn', 'AdjustmentOut') AND \"ReferenceId\" IS NULL)");
                    table.CheckConstraint("CK_InventoryMovements_TotalValueNonNegative", "\"TotalValue\" IS NULL OR \"TotalValue\" >= 0");
                    table.CheckConstraint("CK_InventoryMovements_Type", "\"MovementType\" IN ('OpeningBalance', 'PurchaseIn', 'OrderOut', 'AdjustmentIn', 'AdjustmentOut')");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_BusinessId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "BusinessId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_IngredientId",
                table: "InventoryMovements",
                column: "IngredientId",
                unique: true,
                filter: "\"MovementType\" = 'OpeningBalance' AND \"IngredientId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_IngredientId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "IngredientId", "OccurredAt" },
                filter: "\"IngredientId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType_ReferenceId",
                table: "InventoryMovements",
                columns: new[] { "MovementType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId",
                table: "InventoryMovements",
                column: "ProductId",
                unique: true,
                filter: "\"MovementType\" = 'OpeningBalance' AND \"ProductId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "OccurredAt" },
                filter: "\"ProductId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.AlterColumn<decimal>(
                name: "StockQuantity",
                table: "Products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StockQuantity",
                table: "Ingredients",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);
        }
    }
}
