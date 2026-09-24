using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class M5AddQttCalculationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApplicablePersonalIncomeTaxRate",
                table: "TaxCalculations",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CalculationDataJson",
                table: "TaxCalculations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeductibleExpenses",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPitOverpaid",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPitPaid",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTaxableIncome",
                table: "TaxCalculations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicablePersonalIncomeTaxRate",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "CalculationDataJson",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "TotalDeductibleExpenses",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "TotalPitOverpaid",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "TotalPitPaid",
                table: "TaxCalculations");

            migrationBuilder.DropColumn(
                name: "TotalTaxableIncome",
                table: "TaxCalculations");
        }
    }
}
