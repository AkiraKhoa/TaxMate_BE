using TaxMate.Model.DTO.Reports;

namespace TaxMate.Service.Interfaces;

public interface IReportService
{
    Task<SalesDashboardResponse> GetSalesDashboardAsync(
        Guid businessId,
        int year,
        int month);
}