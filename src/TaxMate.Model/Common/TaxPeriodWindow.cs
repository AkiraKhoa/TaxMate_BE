namespace TaxMate.Model.Common;

public static class S2aHkdErrorCodes
{
    public const string NotEligible = "NotEligible";
    public const string MissingTaxCode = "MissingTaxCode";
    public const string MissingCategory = "MissingCategory";
    public const string NoRevenue = "NoRevenue";
}

public static class TaxPeriodWindow
{
    public static (DateTime Start, DateTime End) GetQuarterWindow(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.");

        var startMonth = ((quarter - 1) * 3) + 1;
        var startDate = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return (startDate, startDate.AddMonths(3));
    }

    public static string FormatQuarterPeriod(int year, int quarter)
    {
        var roman = quarter switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => quarter.ToString()
        };

        return $"Quý {roman}/{year}";
    }

    public static (int Year, int Quarter) GetYearAndQuarter(DateTime utcDate)
    {
        var date = DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
        var quarter = ((date.Month - 1) / 3) + 1;
        return (date.Year, quarter);
    }

    /// <summary>
    /// Current tax quarter plus the previous three quarters (four consecutive periods).
    /// End is exclusive.
    /// </summary>
    public static (DateTime Start, DateTime EndExclusive, int CurrentYear, int CurrentQuarter)
        GetCurrentAndPreviousThreeQuarterWindow(DateTime utcDate)
    {
        var (year, quarter) = GetYearAndQuarter(utcDate);
        var (currentStart, currentEnd) = GetQuarterWindow(year, quarter);
        return (currentStart.AddMonths(-9), currentEnd, year, quarter);
    }
}
