using Microsoft.Extensions.Options;
using TaxMate.Model.DTO.Reports;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;
using TaxMate.Model.Common;
using TaxMate.Service.Common;
namespace TaxMate.Service.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IGenericRepository<TaxMate.Model.Entities.BusinessProfile> _businessProfiles;
    private readonly ITaxPolicyService _taxPolicyService;
    private readonly IOwnerRevenueProjector _ownerRevenue;
    private readonly IUserRepository _users;
    private readonly IGenericRepository<TaxMate.Model.Entities.BusinessCategory> _categories;

    public ReportService(
        IReportRepository reportRepository,
        IGenericRepository<TaxMate.Model.Entities.BusinessProfile> businessProfiles,
        ITaxPolicyService taxPolicyService,
        IOwnerRevenueProjector ownerRevenue,
        IUserRepository users,
        IGenericRepository<TaxMate.Model.Entities.BusinessCategory> categories)
    {
        _reportRepository = reportRepository;
        _businessProfiles = businessProfiles;
        _taxPolicyService = taxPolicyService;
        _ownerRevenue = ownerRevenue;
        _users = users;
        _categories = categories;
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

    var ownerId = business.OwnerId;

    var annual = await _ownerRevenue.ProjectCalendarYearAsync(ownerId, businessId, year);
    var accumulatedRevenue = annual.TotalRevenue;
    var quarters = new List<TaxQuarterRevenueResponse>();
    for (var quarter = 1; quarter <= 4; quarter++)
    {
        var start = annual.StartNaiveUtc.AddMonths((quarter - 1) * 3);
        var end = annual.StartNaiveUtc.AddMonths(quarter * 3);
        var projection = await _ownerRevenue.ProjectAsync(ownerId, businessId, start, end);
        quarters.Add(new TaxQuarterRevenueResponse { Quarter = quarter, Revenue = projection.TotalRevenue });
    }
    var profiles = await _businessProfiles.FindAsync(x => x.OwnerId == ownerId);
    var profileRevenues = new List<OwnerProfileRevenueRow>();
    foreach (var profile in profiles.OrderBy(x => x.BusinessName))
    {
        var projection = await _ownerRevenue.ProjectBusinessAsync(ownerId, profile.Id, annual.StartNaiveUtc, annual.EndExclusiveNaiveUtc);
        profileRevenues.Add(new OwnerProfileRevenueRow { BusinessId = profile.Id, BusinessName = profile.BusinessName, Revenue = projection.TotalRevenue });
    }

    var policyDate = GetPolicyDateForYear(year);
    var policy = await _taxPolicyService.GetEffectiveAsync(policyDate);
    var threshold = policy.AnnualRevenueThreshold;
    var eInvoiceThreshold = policy.EInvoiceRevenueThreshold;

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
            Status = accumulatedRevenue > threshold
                ? "Taxable"
                : "NotTaxable"
        },

        EInvoiceThreshold = new TaxRevenueThresholdResponse
        {
            Amount = eInvoiceThreshold,
            AccumulatedRevenue = accumulatedRevenue,
            RemainingAmount = Math.Max(
                eInvoiceThreshold - accumulatedRevenue,
                0),
            ProgressPercentage = eInvoiceThreshold <= 0
                ? 0
                : Math.Clamp(
                    Math.Round(
                        accumulatedRevenue / eInvoiceThreshold * 100,
                        2),
                    0,
                    100),
            Status = accumulatedRevenue >= eInvoiceThreshold
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

        Quarters = quarters,

        Businesses = profileRevenues
    };
}
    
    public async Task<HomeDashboardResponse> GetHomeDashboardAsync(
    Guid userId,
    Guid businessId,
    DateOnly? date,
    int rangeDays,
    string groupBy)
{
    if (rangeDays < 1 || rangeDays > 365)
    {
        throw new BadRequestException(
            "Range days must be between 1 and 365.");
    }

    var normalizedGroupBy = groupBy
        .Trim()
        .ToLowerInvariant() switch
    {
        "day" => "Day",
        "week" => "Week",
        "month" => "Month",

        _ => throw new BadRequestException(
            "GroupBy must be Day, Week or Month.")
    };

    var business =
        await _businessProfiles.GetByIdAsync(businessId);

    if (business == null)
    {
        throw new NotFoundException(
            "Business profile not found.");
    }

    if (business.OwnerId != userId)
    {
        throw new ForbiddenException(
            "You do not have permission to access this business.");
    }

    var vietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows()
                ? "SE Asia Standard Time"
                : "Asia/Ho_Chi_Minh");

    var vietnamNow =
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            vietnamTimeZone);

    var asOfDate =
        date ?? DateOnly.FromDateTime(vietnamNow);

    var todayStart = new DateTime(
        asOfDate.Year,
        asOfDate.Month,
        asOfDate.Day,
        0,
        0,
        0,
        DateTimeKind.Utc);

    var tomorrowStart = todayStart.AddDays(1);

    var yesterdayStart = todayStart.AddDays(-1);

    var monthStart = new DateTime(
        asOfDate.Year,
        asOfDate.Month,
        1,
        0,
        0,
        0,
        DateTimeKind.Utc);

    var previousMonthStart =
        monthStart.AddMonths(-1);

    var yearStart = new DateTime(
        asOfDate.Year,
        1,
        1,
        0,
        0,
        0,
        DateTimeKind.Utc);

    var trendStart =
        todayStart.AddDays(-(rangeDays - 1));


    // =========================================================
    // TODAY
    // =========================================================

    var today =
        await _reportRepository.GetHomeSalesAggregateAsync(
            businessId,
            todayStart,
            tomorrowStart);

    var yesterday =
        await _reportRepository.GetHomeSalesAggregateAsync(
            businessId,
            yesterdayStart,
            todayStart);


    // =========================================================
    // MONTH PROFIT
    // =========================================================

    var currentMonth =
        await _reportRepository.GetHomeSalesAggregateAsync(
            businessId,
            monthStart,
            tomorrowStart);

    var previousMonth =
        await _reportRepository.GetHomeSalesAggregateAsync(
            businessId,
            previousMonthStart,
            monthStart);

    var currentProfit =
        currentMonth.Revenue -
        currentMonth.CostOfGoodsSold;

    var previousProfit =
        previousMonth.Revenue -
        previousMonth.CostOfGoodsSold;

    var profitMargin =
        currentMonth.Revenue == 0
            ? 0m
            : Math.Round(
                currentProfit /
                currentMonth.Revenue *
                100m,
                2);


    // =========================================================
    // TAX ESTIMATE
    // =========================================================

    var taxContext =
        await _reportRepository
            .GetHomeBusinessTaxContextAsync(
                businessId);

    if (taxContext == null)
    {
        throw new NotFoundException(
            "Business profile not found.");
    }

    if (!taxContext.BusinessCategoryId.HasValue)
    {
        throw new BadRequestException(
            "Business category is required before tax estimation.");
    }

    // Tax estimates use the same business revenue sources and local dates as the books.
    var taxYearStart = BangkokBusinessTime.GetCalendarYearNaiveUtc(asOfDate.Year).Item1;
    var taxMonthStart = monthStart.AddHours(-7);
    var taxEnd = tomorrowStart.AddHours(-7);
    var annual = await _ownerRevenue.ProjectAsync(taxContext.OwnerId, businessId, taxYearStart, taxEnd);
    var previousRevenue = taxMonthStart == taxYearStart ? 0m
        : (await _ownerRevenue.ProjectAsync(taxContext.OwnerId, businessId, taxYearStart, taxMonthStart)).TotalRevenue;
    var ownerMonth = await _ownerRevenue.ProjectAsync(taxContext.OwnerId, businessId, taxMonthStart, taxEnd);
    var businessMonth = await _ownerRevenue.ProjectBusinessAsync(taxContext.OwnerId, businessId, taxMonthStart, taxEnd);
    var owner = await _users.GetByIdAsync(taxContext.OwnerId)
        ?? throw new NotFoundException("Owner not found.");
    var policy = await _taxPolicyService.GetEffectiveAsync(asOfDate);
    var carriedMethod = owner.TaxMethodEffectiveYear < asOfDate.Year &&
        owner.PersonalIncomeTaxMethod is PersonalIncomeTaxMethods.RevenueBased or PersonalIncomeTaxMethods.IncomeBased;
    var isTaxable = annual.TotalRevenue > policy.AnnualRevenueThreshold || carriedMethod;
    decimal vatAmount = 0m;
    decimal pitAmount = 0m;
    if (isTaxable)
    {
        var categories = (await _categories.GetAllAsync()).ToDictionary(x => x.BusinessCategoryId);
        var remaining = owner.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.RevenueBased
            ? Math.Max(0m, policy.AnnualRevenueThreshold - previousRevenue) : 0m;
        var deductions = new Dictionary<Guid, decimal>();
        foreach (var group in ownerMonth.Groups.OrderByDescending(x => categories[x.BusinessCategoryId].PitRate).ThenBy(x => x.BusinessCategoryId))
        {
            var deduction = Math.Min(group.TotalRevenue, remaining);
            deductions[group.BusinessCategoryId] = deduction;
            remaining -= deduction;
        }
        foreach (var group in businessMonth.Groups)
        {
            var total = ownerMonth.Groups.Where(x => x.BusinessCategoryId == group.BusinessCategoryId).Sum(x => x.TotalRevenue);
            // Share the category's one owner-level deduction across its locations.
            var deduction = total > 0m ? deductions.GetValueOrDefault(group.BusinessCategoryId) * group.TotalRevenue / total : 0m;
            vatAmount += group.TotalRevenue * group.VatRate / 100m;
            pitAmount += Math.Max(0m, group.TotalRevenue - deduction) * categories[group.BusinessCategoryId].PitRate / 100m;
        }
        vatAmount = decimal.Round(vatAmount, 2, MidpointRounding.AwayFromZero);
        pitAmount = decimal.Round(pitAmount, 2, MidpointRounding.AwayFromZero);
    }

    // =========================================================
    // REVENUE TREND
    // =========================================================

    var dailyRevenue =
        await _reportRepository.GetHomeDailyRevenueAsync(
            businessId,
            trendStart,
            tomorrowStart);

    var trendPoints =
        BuildHomeRevenueTrend(
            dailyRevenue,
            DateOnly.FromDateTime(trendStart),
            asOfDate,
            normalizedGroupBy);


    // =========================================================
    // REVENUE STRUCTURE
    // =========================================================

    var revenueStructureRows =
        await _reportRepository
            .GetHomeRevenueStructureAsync(
                businessId,
                trendStart,
                tomorrowStart);

    var structureTotal =
        revenueStructureRows.Sum(x => x.Revenue);

    var structureItems =
        revenueStructureRows
            .Select(x =>
                new HomeRevenueStructureItemResponse
                {
                    CategoryId = x.CategoryId,
                    CategoryName = x.CategoryName,
                    Revenue = x.Revenue,

                    Percentage =
                        structureTotal == 0
                            ? 0m
                            : Math.Round(
                                x.Revenue /
                                structureTotal *
                                100m,
                                2)
                })
            .ToList();


    // =========================================================
    // TOP PRODUCTS
    // =========================================================

    var topProducts =
        await _reportRepository.GetHomeTopProductsAsync(
            businessId,
            trendStart,
            tomorrowStart,
            5);


    // =========================================================
    // RESPONSE
    // =========================================================

    return new HomeDashboardResponse
    {
        BusinessId = business.Id,
        BusinessName = business.BusinessName,
        AsOfDate = asOfDate,

        Summary = new HomeDashboardSummaryResponse
        {
            TodayRevenue = new HomeTodayRevenueResponse
            {
                Amount = today.Revenue,

                PreviousDayAmount =
                    yesterday.Revenue,

                ChangePercent =
                    CalculateChangePercent(
                        today.Revenue,
                        yesterday.Revenue)
            },

            TodayOrders = new HomeTodayOrdersResponse
            {
                Count = today.OrderCount,

                PreviousDayCount =
                    yesterday.OrderCount,

                ChangePercent =
                    CalculateChangePercent(
                        today.OrderCount,
                        yesterday.OrderCount),

                AverageOrderValue =
                    today.OrderCount == 0
                        ? 0m
                        : Math.Round(
                            today.Revenue /
                            today.OrderCount,
                            2)
            },

            EstimatedTax = new HomeEstimatedTaxResponse
            {
                PeriodType = "MonthlyEstimate",

                Year = asOfDate.Year,

                Month = asOfDate.Month,

                PeriodLabel =
                    $"Tháng {asOfDate.Month:00}/{asOfDate.Year}",

                IsTaxable = isTaxable,

                TotalAmount =
                    vatAmount + pitAmount,

                Vat = new HomeTaxComponentResponse
                {
                    Rate = taxContext.VatRate,
                    Amount = vatAmount
                },

                PersonalIncomeTax =
                    new HomeTaxComponentResponse
                    {
                        Rate = taxContext.PitRate,
                        Amount = pitAmount
                    }
            },

            EstimatedProfit =
                new HomeEstimatedProfitResponse
                {
                    Year = asOfDate.Year,
                    Month = asOfDate.Month,

                    Amount = currentProfit,

                    PreviousMonthAmount =
                        previousProfit,

                    ChangePercent =
                        CalculateChangePercent(
                            currentProfit,
                            previousProfit),

                    MarginPercent =
                        profitMargin
                }
        },

        RevenueTrend = new HomeRevenueTrendResponse
        {
            FromDate =
                DateOnly.FromDateTime(trendStart),

            ToDate = asOfDate,

            RangeDays = rangeDays,

            GroupBy = normalizedGroupBy,

            Points = trendPoints
        },

        RevenueStructure =
            new HomeRevenueStructureResponse
            {
                TotalRevenue =
                    structureTotal,

                Items =
                    structureItems
            },

        TopProducts =
            new HomeTopProductsResponse
            {
                FromDate =
                    DateOnly.FromDateTime(
                        trendStart),

                ToDate = asOfDate,

                Items = topProducts
            }
    };
}
    
    private static decimal CalculateChangePercent(
        decimal current,
        decimal previous)
    {
        if (previous == 0m)
        {
            return current == 0m
                ? 0m
                : 100m;
        }

        return Math.Round(
            (current - previous) /
            Math.Abs(previous) *
            100m,
            2);
    }

    private static decimal CalculateChangePercent(
        int current,
        int previous)
    {
        return CalculateChangePercent(
            (decimal)current,
            (decimal)previous);
    }
    
    private static List<HomeRevenueTrendPointResponse>
    BuildHomeRevenueTrend(
        List<HomeDailyRevenueRow> dailyRows,
        DateOnly fromDate,
        DateOnly toDate,
        string groupBy)
{
    var grouped = dailyRows
        .GroupBy(x =>
            GetHomeBucketStart(
                DateOnly.FromDateTime(x.Date),
                groupBy))
        .ToDictionary(
            x => x.Key,
            x => x.Sum(y => y.Revenue));

    var points =
        new List<HomeRevenueTrendPointResponse>();

    var cursor =
        GetHomeBucketStart(
            fromDate,
            groupBy);

    var endBucket =
        GetHomeBucketStart(
            toDate,
            groupBy);

    while (cursor <= endBucket)
    {
        points.Add(
            new HomeRevenueTrendPointResponse
            {
                Date = cursor,

                Revenue =
                    grouped.TryGetValue(
                        cursor,
                        out var revenue)
                        ? revenue
                        : 0m
            });

        cursor =
            GetNextHomeBucket(
                cursor,
                groupBy);
    }

    ApplyLinearTrend(points);

    return points;
}


private static DateOnly GetHomeBucketStart(
    DateOnly date,
    string groupBy)
{
    return groupBy switch
    {
        "Day" => date,

        "Week" =>
            date.AddDays(
                -(
                    (
                        (int)date.DayOfWeek -
                        (int)DayOfWeek.Monday +
                        7
                    ) % 7
                )),

        "Month" =>
            new DateOnly(
                date.Year,
                date.Month,
                1),

        _ => date
    };
}


private static DateOnly GetNextHomeBucket(
    DateOnly date,
    string groupBy)
{
    return groupBy switch
    {
        "Day" => date.AddDays(1),

        "Week" => date.AddDays(7),

        "Month" => date.AddMonths(1),

        _ => date.AddDays(1)
    };
}


private static void ApplyLinearTrend(
    List<HomeRevenueTrendPointResponse> points)
{
    if (points.Count == 0)
    {
        return;
    }

    if (points.Count == 1)
    {
        points[0].Trend =
            points[0].Revenue;

        return;
    }

    decimal sumX = 0m;
    decimal sumY = 0m;
    decimal sumXY = 0m;
    decimal sumXX = 0m;

    for (var i = 0; i < points.Count; i++)
    {
        var x = (decimal)i;
        var y = points[i].Revenue;

        sumX += x;
        sumY += y;
        sumXY += x * y;
        sumXX += x * x;
    }

    var n = (decimal)points.Count;

    var denominator =
        n * sumXX -
        sumX * sumX;

    if (denominator == 0m)
    {
        foreach (var point in points)
        {
            point.Trend =
                Math.Round(
                    sumY / n,
                    2);
        }

        return;
    }

    var slope =
        (
            n * sumXY -
            sumX * sumY
        ) / denominator;

    var intercept =
        (
            sumY -
            slope * sumX
        ) / n;

    for (var i = 0; i < points.Count; i++)
    {
        var estimated =
            intercept +
            slope * i;

        points[i].Trend =
            Math.Max(
                0m,
                Math.Round(
                    estimated,
                    2));
    }
}

private static DateOnly GetPolicyDateForYear(int year)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    if (year < today.Year)
    {
        return new DateOnly(year, 12, 31);
    }

    if (year > today.Year)
    {
        return new DateOnly(year, 1, 1);
    }

    return today;
}
}
