namespace TaxMate.Model.DTO.Reports;

public sealed class HomeDashboardResponse
{
    public Guid BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public DateOnly AsOfDate { get; set; }

    public HomeDashboardSummaryResponse Summary { get; set; } = new();

    public HomeRevenueTrendResponse RevenueTrend { get; set; } = new();

    public HomeRevenueStructureResponse RevenueStructure { get; set; } = new();

    public HomeTopProductsResponse TopProducts { get; set; } = new();
}

public sealed class HomeDashboardSummaryResponse
{
    public HomeTodayRevenueResponse TodayRevenue { get; set; } = new();

    public HomeTodayOrdersResponse TodayOrders { get; set; } = new();

    public HomeEstimatedTaxResponse EstimatedTax { get; set; } = new();

    public HomeEstimatedProfitResponse EstimatedProfit { get; set; } = new();
}

public sealed class HomeTodayRevenueResponse
{
    public decimal Amount { get; set; }

    public decimal PreviousDayAmount { get; set; }

    public decimal ChangePercent { get; set; }
}

public sealed class HomeTodayOrdersResponse
{
    public int Count { get; set; }

    public int PreviousDayCount { get; set; }

    public decimal ChangePercent { get; set; }

    public decimal AverageOrderValue { get; set; }
}

public sealed class HomeEstimatedTaxResponse
{
    public string PeriodType { get; set; } = "MonthlyEstimate";

    public int Year { get; set; }

    public int Month { get; set; }

    public string PeriodLabel { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public bool IsTaxable { get; set; }

    public HomeTaxComponentResponse Vat { get; set; } = new();

    public HomeTaxComponentResponse PersonalIncomeTax { get; set; } = new();
}

public sealed class HomeTaxComponentResponse
{
    public decimal Rate { get; set; }

    public decimal Amount { get; set; }
}

public sealed class HomeEstimatedProfitResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Amount { get; set; }

    public decimal PreviousMonthAmount { get; set; }

    public decimal ChangePercent { get; set; }

    public decimal MarginPercent { get; set; }
}

public sealed class HomeRevenueTrendResponse
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public int RangeDays { get; set; }

    public string GroupBy { get; set; } = "Day";

    public List<HomeRevenueTrendPointResponse> Points { get; set; } = [];
}

public sealed class HomeRevenueTrendPointResponse
{
    public DateOnly Date { get; set; }

    public decimal Revenue { get; set; }

    public decimal Trend { get; set; }
}

public sealed class HomeRevenueStructureResponse
{
    public decimal TotalRevenue { get; set; }

    public List<HomeRevenueStructureItemResponse> Items { get; set; } = [];
}

public sealed class HomeRevenueStructureItemResponse
{
    public Guid? CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public decimal Percentage { get; set; }
}

public sealed class HomeTopProductsResponse
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public List<HomeTopProductItemResponse> Items { get; set; } = [];
}

public sealed class HomeTopProductItemResponse
{
    public Guid? ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public decimal QuantitySold { get; set; }
}


// ===== Internal query models used by ReportRepository =====

public sealed class HomeSalesAggregateRow
{
    public decimal Revenue { get; set; }

    public int OrderCount { get; set; }

    public decimal CostOfGoodsSold { get; set; }
}

public sealed class HomeDailyRevenueRow
{
    public DateTime Date { get; set; }

    public decimal Revenue { get; set; }
}

public sealed class HomeRevenueStructureRow
{
    public Guid? CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public decimal Revenue { get; set; }
}

public sealed class HomeBusinessTaxContextRow
{
    public Guid OwnerId { get; set; }

    public Guid? BusinessCategoryId { get; set; }

    public decimal VatRate { get; set; }

    public decimal PitRate { get; set; }
}