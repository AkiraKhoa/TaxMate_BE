using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaxMate.Model.Data;

#nullable disable

namespace TaxMate.Model.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820153000_AddTaxPolicySettings")]
    public partial class AddTaxPolicySettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxPolicySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    AnnualRevenueThreshold = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),
                    EInvoiceRevenueThreshold = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPolicySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicySettings_Year",
                table: "TaxPolicySettings",
                column: "Year",
                unique: true);

            migrationBuilder.InsertData(
                table: "TaxPolicySettings",
                columns:
                [
                    "Id",
                    "Year",
                    "AnnualRevenueThreshold",
                    "EInvoiceRevenueThreshold",
                    "UpdatedByUserId",
                    "CreatedAt",
                    "UpdatedAt"
                ],
                values:
                [
                    new Guid("20260000-0000-4000-a000-000000000001"),
                    2026,
                    1_000_000_000m,
                    1_000_000_000m,
                    null,
                    new DateTime(2026, 8, 20, 0, 0, 0),
                    new DateTime(2026, 8, 20, 0, 0, 0)
                ]);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TaxPolicySettings");
        }
    }
}
