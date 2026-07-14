using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class SeedSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AnnualPrice", "Description", "IsActive", "MaxProducts", "MaxTransactionsPerMonth", "MonthlyPrice", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d"), 0m, "Trải nghiệm các tính năng quản lý cơ bản", true, 50, 100, 0m, "Gói Miễn Phí", 0 },
                    { new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d"), 990000m, "Phù hợp cho hộ kinh doanh cá thể nhỏ", true, 500, 1000, 99000m, "Gói Hộ Kinh Doanh", 1 },
                    { new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d"), 1990000m, "Giải pháp toàn diện cho doanh nghiệp tăng trưởng", true, null, null, 199000m, "Gói Doanh Nghiệp Cao Cấp", 2 }
                });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId" },
                values: new object[,]
                {
                    { new Guid("b1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b7777777-7777-7777-7777-777777777777"), "expense_recording_monitoring", "Ghi nhận & giám sát chi phí", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b8888888-8888-8888-8888-888888888888"), "estimated_profitability_dashboard", "Bảng điều khiển lợi nhuận ước tính", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b9999999-9999-9999-9999-999999999999"), "ai_tax_guidance", "Tư vấn thuế AI", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("baaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "rag_legal_retrieval", "Tra cứu thông tin luật RAG", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "business_insight_reports", "Báo cáo insight kinh doanh", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("e1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e7777777-7777-7777-7777-777777777777"), "expense_recording_monitoring", "Ghi nhận & giám sát chi phí", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e8888888-8888-8888-8888-888888888888"), "estimated_profitability_dashboard", "Bảng điều khiển lợi nhuận ước tính", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e9999999-9999-9999-9999-999999999999"), "ai_tax_guidance", "Tư vấn thuế AI", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("eaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "rag_legal_retrieval", "Tra cứu thông tin luật RAG", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("eaaaaaaa-cccc-cccc-cccc-cccccccccccc"), "einvoice_integration", "Tích hợp hóa đơn điện tử", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ebbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "business_insight_reports", "Báo cáo insight kinh doanh", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ebbbbbbb-dddd-dddd-dddd-dddddddddddd"), "advanced_analytics", "Phân tích kinh doanh nâng cao", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ececcccc-eeee-eeee-eeee-eeeeeeeeeeee"), "growth_readiness_monitoring", "Giám sát mức độ sẵn sàng tăng trưởng", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("f1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b1111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b2222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b3333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b4444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b5555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b6666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b7777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b8888888-8888-8888-8888-888888888888"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("b9999999-9999-9999-9999-999999999999"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("baaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e1111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e2222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e3333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e4444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e5555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e6666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e7777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e8888888-8888-8888-8888-888888888888"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("e9999999-9999-9999-9999-999999999999"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("eaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("eaaaaaaa-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("ebbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("ebbbbbbb-dddd-dddd-dddd-dddddddddddd"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("ececcccc-eeee-eeee-eeee-eeeeeeeeeeee"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f1111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f2222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f3333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f4444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f5555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumn: "Id",
                keyValue: new Guid("f6666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d"));

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d"));

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d"));
        }
    }
}
