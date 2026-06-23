using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessIdToExpenseCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_CategoryName",
                table: "ExpenseCategories");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ExpenseCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_BusinessId",
                table: "ExpenseCategories",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_BusinessId_CategoryName",
                table: "ExpenseCategories",
                columns: new[] { "BusinessId", "CategoryName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseCategories_BusinessProfiles_BusinessId",
                table: "ExpenseCategories",
                column: "BusinessId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseCategories_BusinessProfiles_BusinessId",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_BusinessId",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_BusinessId_CategoryName",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "ExpenseCategories");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_CategoryName",
                table: "ExpenseCategories",
                column: "CategoryName",
                unique: true);
        }
    }
}
