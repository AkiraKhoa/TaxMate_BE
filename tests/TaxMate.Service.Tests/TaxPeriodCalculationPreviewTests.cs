using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;
using Xunit;

namespace TaxMate.Service.Tests;

public class TaxPeriodCalculationPreviewTests
{
    [Fact]
    public async Task GetCalculationPreviewAsync_WhenPeriodIsOpen_CalculatesTaxWithoutChangingStatusOrSavingToDb()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2026,
            TaxProfileConfirmedAt = DateTime.UtcNow
        };

        var category = new BusinessCategory
        {
            BusinessCategoryId = categoryId,
            Code = "CAT01",
            Name = "Kinh doanh hàng hóa",
            VatRate = 1.0m,
            PitRate = 0.5m
        };

        var business = new BusinessProfile
        {
            Id = businessId,
            OwnerId = ownerId,
            Owner = owner,
            MainCategory = category
        };

        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var period = new TaxPeriod
        {
            Id = periodId,
            BusinessId = businessId,
            PeriodType = TaxPeriodTypes.Quarterly,
            Year = 2026,
            Quarter = 1,
            PeriodStartDate = start,
            PeriodEndDate = end,
            Status = TaxPeriodStatuses.Open
        };

        var periods = new Mock<ITaxPeriodRepository>();
        periods.Setup(x => x.GetByIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        periods.Setup(x => x.BusinessBelongsToUserAsync(businessId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        periods.Setup(x => x.GetBusinessWithCategoryAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);

        var calculations = new Mock<ITaxCalculationRepository>();
        var policies = new Mock<ITaxPolicyService>();
        policies.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse
            {
                AnnualRevenueThreshold = 100_000_000m,
                SupportedRevenueCeiling = 50_000_000_000m
            });

        var ownerRevenue = new Mock<IOwnerRevenueProjector>();
        var annualProjection = new OwnerRevenueProjection(
            ownerId, start, end,
            CompletedTransactionRevenue: 200_000_000m,
            ManualBusinessRevenue: 0m,
            Blockers: [])
        {
            Groups = [new OwnerRevenueGroup(categoryId, "CAT01", "Kinh doanh hàng hóa", 1.0m, 200_000_000m, 0m)]
        };

        var periodProjection = new OwnerRevenueProjection(
            ownerId, start, end,
            CompletedTransactionRevenue: 50_000_000m,
            ManualBusinessRevenue: 0m,
            Blockers: [])
        {
            Groups = [new OwnerRevenueGroup(categoryId, "CAT01", "Kinh doanh hàng hóa", 1.0m, 50_000_000m, 0m)]
        };

        ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(ownerId, businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(annualProjection);
        ownerRevenue.Setup(x => x.ProjectAsync(ownerId, businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(periodProjection);

        var service = new TaxPeriodService(
            periods.Object,
            calculations.Object,
            policies.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IAccountingTransactionLockRepository>(),
            Mock.Of<IS2eBookProjector>(),
            Mock.Of<IInventoryMovementRepository>(),
            Mock.Of<IInventoryQuarterFinalizer>(),
            ownerRevenue.Object);

        var result = await service.GetCalculationPreviewAsync(ownerId, periodId);

        Assert.NotNull(result);
        Assert.Equal("Preview", result.Status);
        Assert.Equal(50_000_000m, result.TotalRevenue);
        Assert.Equal(500_000m, result.TotalVatTaxAmount);
        Assert.Single(result.Lines);
        Assert.Equal(500_000m, result.Lines[0].VatTaxAmount);

        Assert.Equal(TaxPeriodStatuses.Open, period.Status);
        calculations.Verify(x => x.AddAsync(It.IsAny<TaxCalculation>()), Times.Never);
        periods.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCalculationPreviewAsync_WhenZeroRevenue_ReturnsCleanPreviewWithoutThrowing()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2026
        };

        var business = new BusinessProfile
        {
            Id = businessId,
            OwnerId = ownerId,
            Owner = owner
        };

        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var period = new TaxPeriod
        {
            Id = periodId,
            BusinessId = businessId,
            PeriodType = TaxPeriodTypes.Quarterly,
            Year = 2026,
            Quarter = 1,
            PeriodStartDate = start,
            PeriodEndDate = end,
            Status = TaxPeriodStatuses.Open
        };

        var periods = new Mock<ITaxPeriodRepository>();
        periods.Setup(x => x.GetByIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        periods.Setup(x => x.BusinessBelongsToUserAsync(businessId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        periods.Setup(x => x.GetBusinessWithCategoryAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);

        var calculations = new Mock<ITaxCalculationRepository>();
        var policies = new Mock<ITaxPolicyService>();
        policies.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse { AnnualRevenueThreshold = 100_000_000m });

        var ownerRevenue = new Mock<IOwnerRevenueProjector>();
        var zeroProjection = new OwnerRevenueProjection(
            ownerId, start, end,
            CompletedTransactionRevenue: 0m,
            ManualBusinessRevenue: 0m,
            Blockers: []);

        ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(ownerId, businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zeroProjection);
        ownerRevenue.Setup(x => x.ProjectAsync(ownerId, businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zeroProjection);

        var service = new TaxPeriodService(
            periods.Object,
            calculations.Object,
            policies.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IAccountingTransactionLockRepository>(),
            Mock.Of<IS2eBookProjector>(),
            Mock.Of<IInventoryMovementRepository>(),
            Mock.Of<IInventoryQuarterFinalizer>(),
            ownerRevenue.Object);

        var result = await service.GetCalculationPreviewAsync(ownerId, periodId);

        Assert.NotNull(result);
        Assert.Equal("Preview", result.Status);
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0m, result.TotalTaxPayableAmount);
        Assert.Empty(result.Lines);
        Assert.Equal(TaxPeriodStatuses.Open, period.Status);
    }
}
