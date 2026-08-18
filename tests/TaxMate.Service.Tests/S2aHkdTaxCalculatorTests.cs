using TaxMate.Model.Common;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class S2aHkdTaxCalculatorTests
{
    [Fact]
    public void CalculateGroupTaxes_GoodsGroup_MatchesFilledSample()
    {
        var (vat, pit) = S2aHkdTaxCalculator.CalculateGroupTaxes(70_000m, 1m, 0.5m);

        Assert.Equal(700m, vat);
        Assert.Equal(350m, pit);
    }

    [Fact]
    public void CalculateGroupTaxes_ServiceGroup_MatchesFilledSample()
    {
        var (vat, pit) = S2aHkdTaxCalculator.CalculateGroupTaxes(600_000m, 5m, 2m);

        Assert.Equal(30_000m, vat);
        Assert.Equal(12_000m, pit);
    }

    [Fact]
    public void CalculateGroupTaxes_FooterTotals_MatchFilledSample()
    {
        var goods = S2aHkdTaxCalculator.CalculateGroupTaxes(70_000m, 1m, 0.5m);
        var service = S2aHkdTaxCalculator.CalculateGroupTaxes(600_000m, 5m, 2m);

        Assert.Equal(30_700m, goods.VatTax + service.VatTax);
        Assert.Equal(12_350m, goods.PitTax + service.PitTax);
    }
}

public class TaxPeriodWindowTests
{
    [Theory]
    [InlineData(2026, 1, 1, 4)]
    [InlineData(2026, 2, 4, 7)]
    [InlineData(2026, 4, 10, 1)]
    public void GetQuarterWindow_ReturnsExpectedBounds(int year, int quarter, int startMonth, int endMonth)
    {
        var (start, end) = TaxPeriodWindow.GetQuarterWindow(year, quarter);

        Assert.Equal(year, start.Year);
        Assert.Equal(startMonth, start.Month);
        Assert.Equal(1, start.Day);
        Assert.Equal(endMonth == 1 ? year + 1 : year, end.Year);
        Assert.Equal(endMonth, end.Month);
        Assert.Equal(1, end.Day);
    }

    [Theory]
    [InlineData(1, "Quý I/2026")]
    [InlineData(2, "Quý II/2026")]
    [InlineData(3, "Quý III/2026")]
    [InlineData(4, "Quý IV/2026")]
    public void FormatQuarterPeriod_ReturnsRomanNumerals(int quarter, string expected)
    {
        Assert.Equal(expected, TaxPeriodWindow.FormatQuarterPeriod(2026, quarter));
    }

    [Fact]
    public void GetCurrentAndPreviousThreeQuarterWindow_UsesCurrentQuarterPlusThreePrevious()
    {
        var asOf = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

        var (start, end, year, quarter) =
            TaxPeriodWindow.GetCurrentAndPreviousThreeQuarterWindow(asOf);

        Assert.Equal(2026, year);
        Assert.Equal(3, quarter);
        Assert.Equal(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void GetCurrentAndPreviousThreeQuarterWindow_Q1IncludesPreviousYear()
    {
        var asOf = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var (start, end, year, quarter) =
            TaxPeriodWindow.GetCurrentAndPreviousThreeQuarterWindow(asOf);

        Assert.Equal(2027, year);
        Assert.Equal(1, quarter);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }
}
