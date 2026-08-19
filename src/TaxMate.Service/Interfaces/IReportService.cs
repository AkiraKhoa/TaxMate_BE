using TaxMate.Model.DTO.Reports;

namespace TaxMate.Service.Interfaces;

public interface IReportService
{
    Task<SalesDashboardResponse> GetSalesDashboardAsync(
        Guid businessId,
        int year,
        int month);
    
    Task<List<BusinessProfileDropdownResponse>>
        GetBusinessesAsync(Guid userId);
    
    Task<List<ActiveSalesMonthResponse>> GetActiveSalesMonthsAsync(
        Guid businessId);
    
    Task<EstimatedProfitDashboardResponse> GetEstimatedProfitDashboardAsync(
        Guid businessId,
        int year,
        int quarter);
    
    Task<List<ActiveSalesQuarterResponse>> GetActiveSalesQuartersAsync(
        Guid businessId);
    
    Task<CashFlowDashboardResponse> GetCashFlowDashboardAsync(
        Guid businessId,
        int year,
        int quarter);
    
    Task<TaxDashboardResponse> GetTaxDashboardAsync(
        Guid businessId,
        int year);
    
    Task<HomeDashboardResponse> GetHomeDashboardAsync(
        Guid userId,
        Guid businessId,
        DateOnly? date,
        int rangeDays,
        string groupBy);
}