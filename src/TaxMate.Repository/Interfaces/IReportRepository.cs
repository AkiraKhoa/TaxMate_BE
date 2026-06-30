using TaxMate.Model.DTO.Reports;

namespace TaxMate.Repository.Interfaces;

public interface IReportRepository
{
    Task<SalesDashboardSummaryResponse> GetSalesSummaryAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate);

    Task<List<ProductRevenueDistributionResponse>> GetRevenueDistributionAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate);

    Task<List<TopSellingProductResponse>> GetTopSellingProductsAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate,
        int top = 3);

    Task<List<SalesTrendResponse>> GetQuarterSalesTrendAsync(
        Guid businessId,
        int year,
        int month);
}