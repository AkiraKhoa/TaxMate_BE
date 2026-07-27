using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class FourthMigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessLocationCode",
                table: "TaxDeclarationObligations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateBudgetContent",
                table: "TaxDeclarationObligations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectingAuthority",
                table: "BusinessProfiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxAuthorityLevel",
                table: "BusinessProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessLocationCode",
                table: "TaxDeclarationObligations");

            migrationBuilder.DropColumn(
                name: "StateBudgetContent",
                table: "TaxDeclarationObligations");

            migrationBuilder.DropColumn(
                name: "CollectingAuthority",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TaxAuthorityLevel",
                table: "BusinessProfiles");
        }
    }
}
