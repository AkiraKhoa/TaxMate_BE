using TaxMate.Model.DTO.Reports;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IGenericRepository<TaxMate.Model.Entities.BusinessProfile> _businessProfiles;

    public ReportService(
        IReportRepository reportRepository,
        IGenericRepository<TaxMate.Model.Entities.BusinessProfile> businessProfiles)
    {
        _reportRepository = reportRepository;
        _businessProfiles = businessProfiles;
    }

    public async Task<SalesDashboardResponse> GetSalesDashboardAsync(
        Guid businessId,
        int year,
        int month)
    {
        if (year <= 0)
        {
            throw new BadRequestException("Year is invalid.");
        }

        if (month < 1 || month > 12)
        {
            throw new BadRequestException("Month must be between 1 and 12.");
        }

        var business = await _businessProfiles.GetByIdAsync(businessId);

        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var startDate = new DateTime(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate
            .AddMonths(1)
            .AddTicks(-1);

        var summary = await _reportRepository
            .GetSalesSummaryAsync(businessId, startDate, endDate);

        var revenueDistribution = await _reportRepository
            .GetRevenueDistributionAsync(businessId, startDate, endDate);

        var topSellingProducts = await _reportRepository
            .GetTopSellingProductsAsync(businessId, startDate, endDate, 3);

        var salesTrend = await _reportRepository
            .GetQuarterSalesTrendAsync(businessId, year, month);

        return new SalesDashboardResponse
        {
            Period = new ReportPeriodResponse
            {
                Year = year,
                Month = month,
                Label = $"Tháng {month}/{year}",
                StartDate = startDate,
                EndDate = endDate
            },
            Summary = summary,
            RevenueDistribution = revenueDistribution,
            TopSellingProducts = topSellingProducts,
            SalesTrend = salesTrend
        };
    }
}