using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class SecondMigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnnualRevenueAtCalculation",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ApplicableRevenueThreshold",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedFormCode",
                table: "TaxCalculations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessLocationCode",
                table: "BusinessProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagingTaxAuthority",
                table: "BusinessProfiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxAdministrationAreaCode",
                table: "BusinessProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "FormIndicatorCode",
                value: "d");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d2222222-2222-2222-2222-222222222222"),
                column: "FormIndicatorCode",
                value: "b");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualRevenueAtCalculation",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "ApplicableRevenueThreshold",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "RecommendedFormCode",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "BusinessLocationCode",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "ManagingTaxAuthority",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TaxAdministrationAreaCode",
                table: "BusinessProfiles");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d1111111-1111-1111-1111-111111111111"),
                column: "FormIndicatorCode",
                value: "08d");

            migrationBuilder.UpdateData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("d2222222-2222-2222-2222-222222222222"),
                column: "FormIndicatorCode",
                value: "08b");
        }
    }
}
