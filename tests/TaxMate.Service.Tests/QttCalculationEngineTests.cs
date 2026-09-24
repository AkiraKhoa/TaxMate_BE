using TaxMate.Model.Common;
using TaxMate.Model.DTO.Tax;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Services;
using Xunit;

namespace TaxMate.Service.Tests;

public class QttCalculationEngineTests
{
    [Fact]
    public void Calculate_WhenQuartersAreNotClosed_StillCalculatesIndicators09To24()
    {
        var preview = new QttPreviewResponse
        {
            TaxYear = 2026,
            Eligibility = QttEligibility.NormalIncomeBased,
            Revenue = new QttRevenueBreakdown(2_000_000_000m, 0m, 0m),
            Expenses = new QttExpenseBreakdown(1_000_000_000m, 300_000_000m, 0m, 200_000_000m, 0m, 0m, 0m, 0m),
            PitPayments = new QttPitPaymentBreakdown { Indicator15 = 20_000_000m },
            Inventory = new QttInventorySummary
            {
                Indicator31OpeningValue = 100_000_000m,
                Indicator32InboundValue = 500_000_000m,
                Indicator33OutboundValue = 400_000_000m,
                Indicator34EndingValue = 200_000_000m
            },
            HardBlockers = [new QttPreviewIssue("Quarter4NotClosed", "Quý 4 chưa đóng")],
            Warnings = []
        };

        var engine = new QttCalculationEngine();

        // ACT
        var result = engine.Calculate(preview);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(2026, result.TaxYear);
        Assert.Equal(2_000_000_000m, result.Indicators.Indicator09);
        Assert.Equal(1_500_000_000m, result.Indicators.Indicator10);
        Assert.Equal(500_000_000m, result.Indicators.Indicator11); // 2B - 1.5B = 500M
        Assert.Equal(15m, result.Indicators.Indicator12Rate);
        Assert.Equal(75_000_000m, result.Indicators.Indicator13); // 500M * 15% = 75M
        Assert.Equal(20_000_000m, result.Indicators.Indicator15);
        Assert.Equal(55_000_000m, result.Indicators.Indicator19); // 75M - 20M = 55M payable
        Assert.Equal(QttCalculationOutcomes.Payable, result.Outcome);

        // Passthrough of readiness & blockers
        Assert.False(result.CanClose);
        Assert.Single(result.HardBlockers);
        Assert.Equal("Quarter4NotClosed", result.HardBlockers[0].Code);
    }

    [Fact]
    public void Calculate_WhenRevenueOver50B_ThrowsConflictException()
    {
        var preview = new QttPreviewResponse
        {
            TaxYear = 2026,
            Eligibility = QttEligibility.NormalIncomeBased,
            Revenue = new QttRevenueBreakdown(55_000_000_000m, 0m, 0m),
            Expenses = new QttExpenseBreakdown(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            PitPayments = new QttPitPaymentBreakdown(),
            Inventory = new QttInventorySummary()
        };

        var engine = new QttCalculationEngine();

        Assert.Throws<ConflictException>(() => engine.Calculate(preview));
    }

    [Fact]
    public void Calculate_WhenUnderOneBillionRefund_ResolvesZeroRate()
    {
        var preview = new QttPreviewResponse
        {
            TaxYear = 2026,
            Eligibility = QttEligibility.UnderOneBillionRefund,
            Revenue = new QttRevenueBreakdown(800_000_000m, 0m, 0m),
            Expenses = new QttExpenseBreakdown(500_000_000m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            PitPayments = new QttPitPaymentBreakdown { Indicator15 = 10_000_000m },
            Inventory = new QttInventorySummary()
        };

        var engine = new QttCalculationEngine();

        var result = engine.Calculate(preview);

        Assert.Equal(0m, result.Indicators.Indicator12Rate);
        Assert.Equal(0m, result.Indicators.Indicator13);
        Assert.Equal(10_000_000m, result.Indicators.Indicator20); // Overpaid: 10M
        Assert.Equal(QttCalculationOutcomes.Overpaid, result.Outcome);
    }
}
