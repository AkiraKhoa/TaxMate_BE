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
}