using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M6AddInventoryMovementSourceIdempotency : Migration
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
                        FROM "InventoryMovements"
                        WHERE "ReferenceId" IS NOT NULL
                          AND "ProductId" IS NOT NULL
                        GROUP BY "MovementType", "ReferenceId", "ProductId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'InventoryMovements contains duplicate product source lines; aggregate each MovementType/ReferenceId/ProductId before M6.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM "InventoryMovements"
                        WHERE "ReferenceId" IS NOT NULL
                          AND "IngredientId" IS NOT NULL
                        GROUP BY "MovementType", "ReferenceId", "IngredientId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'InventoryMovements contains duplicate ingredient source lines; aggregate each MovementType/ReferenceId/IngredientId before M6.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType_ReferenceId_IngredientId",
                table: "InventoryMovements",
                columns: new[] { "MovementType", "ReferenceId", "IngredientId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL AND \"IngredientId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType_ReferenceId_ProductId",
                table: "InventoryMovements",
                columns: new[] { "MovementType", "ReferenceId", "ProductId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL AND \"ProductId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_MovementType_ReferenceId_IngredientId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_MovementType_ReferenceId_ProductId",
                table: "InventoryMovements");
        }
    }
}
