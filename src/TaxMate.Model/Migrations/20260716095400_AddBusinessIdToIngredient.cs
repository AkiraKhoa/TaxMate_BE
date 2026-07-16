using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessIdToIngredient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Ingredients" AS i
                SET "BusinessId" = mapped."BusinessId"
                FROM (
                    SELECT
                        pi."IngredientId",
                        (array_agg(p."BusinessId"))[1] AS "BusinessId"
                    FROM "ProductIngredients" AS pi
                    INNER JOIN "Products" AS p ON p."Id" = pi."ProductId"
                    GROUP BY pi."IngredientId"
                    HAVING COUNT(DISTINCT p."BusinessId") = 1
                ) AS mapped
                WHERE i."Id" = mapped."IngredientId"
                  AND i."BusinessId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Ingredients"
                SET "BusinessId" = (
                    SELECT bp."Id"
                    FROM "BusinessProfiles" AS bp
                    ORDER BY bp."CreatedAt"
                    LIMIT 1
                )
                WHERE "BusinessId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "Ingredients",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_BusinessId",
                table: "Ingredients",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_BusinessId_Name",
                table: "Ingredients",
                columns: new[] { "BusinessId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_BusinessProfiles_BusinessId",
                table: "Ingredients",
                column: "BusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_BusinessProfiles_BusinessId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_BusinessId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_BusinessId_Name",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Ingredients");
        }
    }
}
