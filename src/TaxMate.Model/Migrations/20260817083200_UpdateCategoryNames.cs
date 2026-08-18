using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCategoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockTrackingEnabled",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000003"),
                column: "Name",
                value: "Dịch vụ");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "FNB");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsStockTrackingEnabled",
                table: "BusinessProfiles");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000003"),
                column: "Name",
                value: "Dịch vụ");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "F&B");
        }
    }
}
