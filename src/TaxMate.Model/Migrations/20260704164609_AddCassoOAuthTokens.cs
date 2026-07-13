using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddCassoOAuthTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CassoAccessToken",
                table: "PaymentAccounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CassoConnectedAccountId",
                table: "PaymentAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CassoRefreshToken",
                table: "PaymentAccounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CassoAccessToken",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "CassoConnectedAccountId",
                table: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "CassoRefreshToken",
                table: "PaymentAccounts");
        }
    }
}
