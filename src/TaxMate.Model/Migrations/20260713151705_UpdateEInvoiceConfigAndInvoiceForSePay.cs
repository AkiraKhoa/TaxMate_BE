using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEInvoiceConfigAndInvoiceForSePay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "EInvoiceConfigs");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "EInvoiceConfigs");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "EInvoiceConfigs");

            migrationBuilder.RenameColumn(
                name: "ApiUrl",
                table: "EInvoiceConfigs",
                newName: "ClientSecret");

            migrationBuilder.AddColumn<string>(
                name: "SePayMessage",
                table: "Invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SePayReferenceCode",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SePayTrackingCode",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "EInvoiceConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "EInvoiceConfigs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderAccountId",
                table: "EInvoiceConfigs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SePayMessage",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SePayReferenceCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SePayTrackingCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "EInvoiceConfigs");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "EInvoiceConfigs");

            migrationBuilder.DropColumn(
                name: "ProviderAccountId",
                table: "EInvoiceConfigs");

            migrationBuilder.RenameColumn(
                name: "ClientSecret",
                table: "EInvoiceConfigs",
                newName: "ApiUrl");

            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "EInvoiceConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "EInvoiceConfigs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "EInvoiceConfigs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
