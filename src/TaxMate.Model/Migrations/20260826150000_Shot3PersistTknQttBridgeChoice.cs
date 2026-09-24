using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TaxMate.Model.Data;

#nullable disable

namespace TaxMate.Model.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260826150000_Shot3PersistTknQttBridgeChoice")]
    public partial class Shot3PersistTknQttBridgeChoice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TknQttBridgeChoice",
                table: "TaxPeriods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TknQttBridgeChoiceAt",
                table: "TaxPeriods",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxPeriods_TknQttBridgeChoice",
                table: "TaxPeriods",
                sql: "(\"TknQttBridgeChoice\" IS NULL AND \"TknQttBridgeChoiceAt\" IS NULL) OR " +
                     "(\"PeriodType\" = 'Tkn' AND \"TknQttBridgeChoice\" IN ('Later', 'Refund', 'Offset') AND \"TknQttBridgeChoiceAt\" IS NOT NULL)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxPeriods_TknQttBridgeChoice",
                table: "TaxPeriods");

            migrationBuilder.DropColumn(
                name: "TknQttBridgeChoice",
                table: "TaxPeriods");

            migrationBuilder.DropColumn(
                name: "TknQttBridgeChoiceAt",
                table: "TaxPeriods");
        }
    }
}
