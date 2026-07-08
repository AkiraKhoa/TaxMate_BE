namespace TaxMate.Model.DTO.Dashboard;

public class MomCountMetricDto
{
    public int CurrentMonth { get; set; }

    public int LastMonth { get; set; }

    public decimal? DeltaPercent { get; set; }
}

public class MomRevenueMetricDto
{
    public decimal CurrentMonth { get; set; }

    public decimal LastMonth { get; set; }

    public decimal? DeltaPercent { get; set; }
}

public class MonthlyTrendPointDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public int Value { get; set; }
}

public class SubscriptionTrendResponseDto
{
    public List<MonthlyTrendPointDto> Points { get; set; } = [];
}

public class BusinessUserTrendResponseDto
{
    public List<MonthlyTrendPointDto> Points { get; set; } = [];
}

public class PackageDistributionItemDto
{
    public Guid PlanId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class MonthlyPackageDistributionDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public List<PackageDistributionItemDto> Packages { get; set; } = [];
}

public class ServicePackageDistributionResponseDto
{
    public List<MonthlyPackageDistributionDto> Months { get; set; } = [];
}

public class PackageRevenueItemDto
{
    public Guid PlanId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public int SubscriptionCount { get; set; }

    public decimal Revenue { get; set; }
}

public class PackageRevenueResponseDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public decimal TotalRevenue { get; set; }

    public List<PackageRevenueItemDto> Packages { get; set; } = [];
}
