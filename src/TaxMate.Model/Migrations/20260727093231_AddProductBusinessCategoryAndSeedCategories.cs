using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBusinessCategoryAndSeedCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessCategoryId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.InsertData(
                table: "BusinessCategories",
                columns: new[] { "BusinessCategoryId", "Code", "CreatedAt", "Description", "Name", "PitRate", "UpdatedAt", "VatRate" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-4000-8000-000000000001"), "DIST_GOODS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 1%, TNCN 0.5%", "Phân phối, cung cấp hàng hóa", 0.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m },
                    { new Guid("a0000001-0000-4000-8000-000000000002"), "PROD_TRANSPORT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 3%, TNCN 1.5%", "Sản xuất, vận tải, dịch vụ gắn HH, XD có NVL", 1.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3m },
                    { new Guid("a0000001-0000-4000-8000-000000000003"), "SERVICE_CONSTRUCT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 5%, TNCN 2%", "Dịch vụ, XD không bao thầu NVL", 2m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5m },
                    { new Guid("a0000001-0000-4000-8000-000000000004"), "ASSET_INSURANCE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 5%, TNCN 5%", "Cho thuê tài sản / đại lý BH, xổ số, BHĐC…", 5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5m },
                    { new Guid("a0000001-0000-4000-8000-000000000005"), "OTHER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 2%, TNCN 1%", "Hoạt động khác", 1m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessCategoryId",
                table: "Products",
                column: "BusinessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_BusinessCategoryId",
                table: "Products",
                columns: new[] { "BusinessId", "BusinessCategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_BusinessCategories_BusinessCategoryId",
                table: "Products",
                column: "BusinessCategoryId",
                principalTable: "BusinessCategories",
                principalColumn: "BusinessCategoryId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_BusinessCategories_BusinessCategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessCategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId_BusinessCategoryId",
                table: "Products");

            migrationBuilder.DeleteData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "BusinessCategories",
                keyColumn: "BusinessCategoryId",
                keyValue: new Guid("a0000001-0000-4000-8000-000000000005"));

            migrationBuilder.DropColumn(
                name: "BusinessCategoryId",
                table: "Products");
        }
    }
}
