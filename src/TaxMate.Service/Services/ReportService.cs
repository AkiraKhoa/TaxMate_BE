using Microsoft.Extensions.Options;
using TaxMate.Model.DTO.Reports;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;

namespace TaxMate.Service.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IGenericRepository<TaxMate.Model.Entities.BusinessProfile> _businessProfiles;
    private readonly IOptions<TaxSettings> _taxSettings;

    public ReportService(
        IReportRepository reportRepository,
        IGenericRepository<TaxMate.Model.Entities.BusinessProfile> businessProfiles,
        IOptions<TaxSettings> taxSettings)
    {
        _reportRepository = reportRepository;
        _businessProfiles = businessProfiles;
        _taxSettings = taxSettings;
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
    
    public async Task<List<BusinessProfileDropdownResponse>> GetBusinessesAsync(
        Guid userId)
    {
        var businesses = await _businessProfiles.FindAsync(x =>
            x.OwnerId == userId);

        return businesses
            .OrderBy(x => x.BusinessName)
            .Select(x => new BusinessProfileDropdownResponse
            {
                Id = x.Id,
                BusinessName = x.BusinessName
            })
            .ToList();
    }
    
    public async Task<List<ActiveSalesMonthResponse>> GetActiveSalesMonthsAsync(
        Guid businessId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);

        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        return await _reportRepository
            .GetActiveSalesMonthsAsync(businessId);
    }
    
    public async Task<EstimatedProfitDashboardResponse> GetEstimatedProfitDashboardAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        if (year <= 0)
        {
            throw new BadRequestException("Year is invalid.");
        }

        if (quarter < 1 || quarter > 4)
        {
            throw new BadRequestException("Quarter must be between 1 and 4.");
        }

        var business = await _businessProfiles.GetByIdAsync(businessId);

        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var startMonth = ((quarter - 1) * 3) + 1;
        var endMonth = startMonth + 2;

        var summary = await _reportRepository
            .GetEstimatedProfitSummaryAsync(
                businessId,
                year,
                quarter);

        var profitTrend = await _reportRepository
            .GetEstimatedProfitTrendAsync(
                businessId,
                year);

        return new EstimatedProfitDashboardResponse
        {
            Period = new EstimatedProfitPeriodResponse
            {
                Year = year,
                Quarter = quarter,
                Label = $"Quý {ToRomanQuarter(quarter)}/{year} ({startMonth:00}-{endMonth:00}/{year})",
                StartMonth = startMonth,
                EndMonth = endMonth
            },
            Summary = summary,
            ProfitTrend = profitTrend
        };
    }
    
    private static string ToRomanQuarter(int quarter)
    {
        return quarter switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => quarter.ToString()
        };
    }
    
    public async Task<List<ActiveSalesQuarterResponse>> GetActiveSalesQuartersAsync(
        Guid businessId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);

        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        return await _reportRepository
            .GetActiveSalesQuartersAsync(businessId);
    }
    
    public async Task<CashFlowDashboardResponse> GetCashFlowDashboardAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        if (year <= 0)
        {
            throw new BadRequestException("Year is invalid.");
        }

        if (quarter < 1 || quarter > 4)
        {
            throw new BadRequestException("Quarter must be between 1 and 4.");
        }

        var business = await _businessProfiles.GetByIdAsync(businessId);

        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var startMonth = ((quarter - 1) * 3) + 1;
        var endMonth = startMonth + 2;

        var summary = await _reportRepository
            .GetCashFlowSummaryAsync(businessId, year, quarter);

        var expenseDistribution = await _reportRepository
            .GetExpenseDistributionAsync(businessId, year, quarter);

        var cashFlowTrend = await _reportRepository
            .GetCashFlowTrendAsync(businessId, year, quarter);

        return new CashFlowDashboardResponse
        {
            Period = new CashFlowPeriodResponse
            {
                Year = year,
                Quarter = quarter,
                Label = $"Quý {ToRomanQuarter(quarter)}/{year} ({startMonth:00}-{endMonth:00}/{year})",
                StartMonth = startMonth,
                EndMonth = endMonth
            },
            Summary = summary,
            ExpenseDistribution = expenseDistribution,
            CashFlowTrend = cashFlowTrend
        };
    }
    public async Task<TaxDashboardResponse> GetTaxDashboardAsync(
    Guid businessId,
    int year)
{
    if (year <= 0)
    {
        throw new BadRequestException("Year is invalid.");
    }

    var business = await _businessProfiles.GetByIdAsync(businessId);

    if (business == null)
    {
        throw new NotFoundException("Business profile not found.");
    }

    var accumulatedRevenue =
        await _reportRepository.GetAccumulatedRevenueAsync(
            businessId,
            year);

    var quarters =
        await _reportRepository.GetQuarterRevenuesAsync(
            businessId,
            year);

    var threshold =
        _taxSettings.Value.BusinessRevenueThreshold;

    var progress = threshold <= 0
        ? 0
        : Math.Round(
            accumulatedRevenue / threshold * 100,
            2);

    progress = Math.Clamp(progress, 0, 100);

    var remaining = Math.Max(
        threshold - accumulatedRevenue,
        0);

    var utcNow = DateTime.UtcNow;

    var currentQuarter = year switch
    {
        _ when year < utcNow.Year => 4,
        _ when year > utcNow.Year => 0,
        _ => ((utcNow.Month - 1) / 3) + 1
    };

    foreach (var item in quarters)
    {
        item.Status = year switch
        {
            _ when year < utcNow.Year => "Completed",
            _ when year > utcNow.Year => "Upcoming",
            _ when item.Quarter < currentQuarter => "Completed",
            _ when item.Quarter == currentQuarter => "Current",
            _ => "Upcoming"
        };
    }

    var latestQuarterWithRevenue = quarters
        .Where(x => x.Revenue > 0)
        .Select(x => x.Quarter)
        .DefaultIfEmpty(0)
        .Max();

    decimal forecast;

    if (latestQuarterWithRevenue == 0)
    {
        forecast = 0;
    }
    else if (latestQuarterWithRevenue == 4)
    {
        forecast = accumulatedRevenue;
    }
    else
    {
        forecast = Math.Round(
            accumulatedRevenue / latestQuarterWithRevenue * 4,
            0);
    }

    return new TaxDashboardResponse
    {
        Year = year,

        Threshold = new TaxRevenueThresholdResponse
        {
            Amount = threshold,
            AccumulatedRevenue = accumulatedRevenue,
            RemainingAmount = remaining,
            ProgressPercentage = progress,
            Status = accumulatedRevenue >= threshold
                ? "RequiredEInvoice"
                : "NotRequired"
        },

        Forecast = new TaxRevenueForecastResponse
        {
            EstimatedYearEndRevenue = forecast,
            BasedOnThroughQuarter = latestQuarterWithRevenue,
            Label = latestQuarterWithRevenue == 0
                ? "Chưa có dữ liệu"
                : latestQuarterWithRevenue == 1
                    ? "Dựa trên Q1"
                    : $"Dựa trên Q1 - Q{latestQuarterWithRevenue}"
        },

        Quarters = quarters
    };
}
}