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
    
    Task<List<ActiveSalesMonthResponse>> GetActiveSalesMonthsAsync(
        Guid businessId);
    
    Task<EstimatedProfitSummaryResponse> GetEstimatedProfitSummaryAsync(
        Guid businessId,
        int year,
        int quarter);

    Task<List<EstimatedProfitTrendResponse>> GetEstimatedProfitTrendAsync(
        Guid businessId,
        int year);
    
    Task<List<ActiveSalesQuarterResponse>> GetActiveSalesQuartersAsync(
        Guid businessId);
    Task<CashFlowSummaryResponse> GetCashFlowSummaryAsync(
        Guid businessId,
        int year,
        int quarter);

    Task<List<ExpenseDistributionResponse>> GetExpenseDistributionAsync(
        Guid businessId,
        int year,
        int quarter);

    Task<List<CashFlowTrendResponse>> GetCashFlowTrendAsync(
        Guid businessId,
        int year,
        int quarter);
    
    Task<decimal> GetAccumulatedRevenueAsync(
        Guid businessId,
        int year);

    Task<List<TaxQuarterRevenueResponse>> GetQuarterRevenuesAsync(
        Guid businessId,
        int year);

    Task<List<OwnerProfileRevenueRow>> GetOwnerRevenueByProfileAsync(
        Guid ownerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}