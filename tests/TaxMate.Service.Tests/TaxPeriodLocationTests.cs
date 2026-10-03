using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;
using Xunit;

namespace TaxMate.Service.Tests;

public class TaxPeriodLocationTests
{
    [Theory]
    [InlineData(TknFilingWindows.FirstHalf)]
    [InlineData(TknFilingWindows.SecondHalf)]
    [InlineData(TknFilingWindows.Annual)]
    public void DeclarationCode_RemainsUniqueAcrossOwnersAndFitsTheDatabaseColumn(string window)
    {
        var buildCode = typeof(TaxDeclarationService).GetMethod("BuildDeclarationCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var first = new TaxPeriod { Id = Guid.NewGuid(), Year = 2026, PeriodType = TaxPeriodTypes.Tkn, FilingWindow = window };
        var second = new TaxPeriod { Id = Guid.NewGuid(), Year = 2026, PeriodType = TaxPeriodTypes.Tkn, FilingWindow = window };
        var code = (string)buildCode.Invoke(null, [first, 1])!;
        Assert.NotEqual(code, (string)buildCode.Invoke(null, [second, 1])!);
        Assert.NotEqual(code, (string)buildCode.Invoke(null, [first, 2])!);
        Assert.True(code.Length <= 50);
    }

    [Fact]
    public async Task Projection_KeepsLocationsAndManualRevenueWithoutChangingCategoryTotals()
    {
        var f = new Fixture();
        var projection = await f.Revenue.ProjectCalendarYearAsync(f.Owner.Id, f.Businesses[0].Id, 2026);
        Assert.Equal(2_000_000_000m, projection.TotalRevenue);
        Assert.Equal(2, projection.Groups.Count);
        Assert.Equal(3, projection.LocationGroups.Count);
        Assert.Equal(50_000_000m, projection.LocationGroups.Single(x => x.BusinessId == f.Businesses[0].Id).ManualBusinessRevenue);
        Assert.Equal(1_400_000_000m, projection.Groups.Single(x => x.BusinessCategoryCode == "FNB").TotalRevenue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Calculation_SharesOnePoolPrioritizesServiceAndKeepsTwoLocationsWithSameCategory(bool persist)
    {
        var f = new Fixture();
        var result = persist
            ? await f.Service.CalculateAsync(f.Owner.Id, f.Period.Id)
            : await f.Service.GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(3, result.Lines.Select(x => x.BusinessLocationId).Distinct().Count());
        Assert.All(result.Lines, x => Assert.Equal(
            f.Businesses.Single(b => b.Id == x.BusinessLocationId).BusinessLocationCode, x.BusinessLocationCode));
        Assert.Equal(600_000_000m, result.Lines.Single(x => x.BusinessActivityCode == "SERVICE").PersonalIncomeTaxDeductibleRevenue);
        Assert.Equal(400_000_000m, result.Lines.Where(x => x.BusinessActivityCode == "FNB").Sum(x => x.PersonalIncomeTaxDeductibleRevenue));
        Assert.Equal(1_000_000_000m, result.Lines.Sum(x => x.PersonalIncomeTaxDeductibleRevenue));
        Assert.Equal(72_000_000m, result.TotalVatTaxAmount);
        Assert.Equal(15_000_000m, result.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, result.RemainingPitDeduction);
        if (persist)
        {
            Assert.NotNull(f.Saved);
            Assert.All(f.Saved!.Lines, x => Assert.NotNull(x.BusinessLocationId));
            Assert.Equal(result.TotalPersonalIncomeTaxAmount, f.Saved.Lines.Sum(x => x.PersonalIncomeTaxAmount));
        }
        else
        {
            Assert.Null(f.Saved);
            f.Periods.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewTaxEffectiveYear_BlocksQuartersBeforeCrossingForBothPaths(bool persist)
    {
        var f = new Fixture(revenueQuarter: 3);
        f.Owner.TaxMethodEffectiveYear = 2026;
        var error = await Assert.ThrowsAsync<ConflictException>(() => persist
            ? f.Service.CalculateAsync(f.Owner.Id, f.Period.Id)
            : f.Service.GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id));
        Assert.Contains("quý 3", error.Message);
        Assert.Null(f.Saved);
    }

    private sealed class Fixture
    {
        public User Owner { get; } = new()
        {
            Id = Guid.NewGuid(), PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2025, TaxProfileConfirmedAt = DateTime.UtcNow
        };
        public List<BusinessProfile> Businesses { get; }
        public TaxPeriod Period { get; }
        public Mock<ITaxPeriodRepository> Periods { get; } = new();
        public OwnerRevenueProjector Revenue { get; }
        public TaxPeriodService Service { get; }
        public TaxCalculation? Saved { get; private set; }

        public Fixture(int revenueQuarter = 1)
        {
            var service = new BusinessCategory { BusinessCategoryId = Guid.NewGuid(), Code = "SERVICE", Name = "Dịch vụ", VatRate = 5m, PitRate = 2m };
            var fnb = new BusinessCategory { BusinessCategoryId = Guid.NewGuid(), Code = "FNB", Name = "Ăn uống", VatRate = 3m, PitRate = 1.5m };
            Businesses = new[] { service, fnb, fnb }.Select((c, i) => new BusinessProfile
            {
                Id = Guid.NewGuid(), OwnerId = Owner.Id, Owner = Owner, MainCategoryId = c.BusinessCategoryId,
                MainCategory = c, BusinessLocationCode = $"SITE-{i + 1}"
            }).ToList();
            var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
            Period = new TaxPeriod { Id = Guid.NewGuid(), BusinessId = Businesses[0].Id, Year = 2026, Quarter = 1,
                PeriodType = TaxPeriodTypes.Quarterly, PeriodStartDate = start, PeriodEndDate = end, Status = TaxPeriodStatuses.Closed };
            var date = BangkokBusinessTime.GetQuarterNaiveUtc(2026, revenueQuarter).StartNaiveUtc.AddDays(10);
            var reads = new Mock<IAccountingScopeReadRepository>();
            reads.Setup(x => x.ResolveOwnerScopeAsync(Businesses[0].Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OwnerBusinessScope(Owner.Id, Businesses.Select(x => x.Id).ToHashSet()));
            var amounts = new[] { 550_000_000m, 800_000_000m, 600_000_000m };
            reads.Setup(x => x.GetRevenueTransactionsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Businesses.Select((b, i) => new RevenueTransactionSource(b.Id, Guid.NewGuid(), TransactionTypes.Sale, TransactionStatus.Completed, date, amounts[i], true)
                { BusinessCategoryId = b.MainCategoryId, BusinessCategoryCode = b.MainCategory!.Code, BusinessCategoryName = b.MainCategory.Name, VatRate = b.MainCategory.VatRate }).ToArray());
            reads.Setup(x => x.GetRevenueIncomesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new RevenueIncomeSource(Businesses[0].Id, Guid.NewGuid(), null, IncomeAccountingTypes.BusinessRevenue, date, 50_000_000m)
                { BusinessCategoryId = service.BusinessCategoryId, BusinessCategoryCode = service.Code, BusinessCategoryName = service.Name, VatRate = service.VatRate } });
            Revenue = new OwnerRevenueProjector(reads.Object);
            Periods.Setup(x => x.GetByIdAsync(Period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
            Periods.Setup(x => x.BusinessBelongsToUserAsync(Period.BusinessId, Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Periods.Setup(x => x.GetBusinessWithCategoryAsync(Period.BusinessId, It.IsAny<CancellationToken>())).ReturnsAsync(Businesses[0]);
            Periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Businesses);
            var calculations = new Mock<ITaxCalculationRepository>();
            calculations.Setup(x => x.AddAsync(It.IsAny<TaxCalculation>())).Callback<TaxCalculation>(x => Saved = x).Returns(Task.CompletedTask);
            var policies = new Mock<ITaxPolicyService>();
            policies.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EffectiveTaxPolicyResponse { AnnualRevenueThreshold = 1_000_000_000m, SupportedRevenueCeiling = 50_000_000_000m });
            Service = new TaxPeriodService(Periods.Object, calculations.Object, policies.Object, Mock.Of<IUnitOfWork>(),
                Mock.Of<IAccountingTransactionLockRepository>(), Mock.Of<IS2eBookProjector>(), Mock.Of<IInventoryMovementRepository>(),
                Mock.Of<IInventoryQuarterFinalizer>(), Revenue);
        }
    }
}
