using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
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
    public async Task GetCalculationPreviewAsync_WhenCarriedMethodAndZeroRevenue_ReturnsCleanPreviewWithoutThrowing()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2025 // Carried method from previous year
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
            .ReturnsAsync(new EffectiveTaxPolicyResponse { AnnualRevenueThreshold = 100_000_000m, SupportedRevenueCeiling = 50_000_000_000m });

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

    [Fact]
    public async Task GetCalculationPreviewAsync_WhenNewAccountBelowAnnualThreshold_ThrowsConflictException()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2026 // New account elected in current year
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

        var policies = new Mock<ITaxPolicyService>();
        policies.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse { AnnualRevenueThreshold = 1_000_000_000m, SupportedRevenueCeiling = 50_000_000_000m });

        var ownerRevenue = new Mock<IOwnerRevenueProjector>();
        var subThresholdProjection = new OwnerRevenueProjection(
            ownerId, start, end,
            CompletedTransactionRevenue: 500_000_000m,
            ManualBusinessRevenue: 0m,
            Blockers: []);

        ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(ownerId, businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subThresholdProjection);

        var service = new TaxPeriodService(
            periods.Object,
            Mock.Of<ITaxCalculationRepository>(),
            policies.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IAccountingTransactionLockRepository>(),
            Mock.Of<IS2eBookProjector>(),
            Mock.Of<IInventoryMovementRepository>(),
            Mock.Of<IInventoryQuarterFinalizer>(),
            ownerRevenue.Object);

        await Assert.ThrowsAsync<ConflictException>(() => service.GetCalculationPreviewAsync(ownerId, periodId));
    }

    [Fact]
    public async Task GetCalculationPreviewAsync_WhenNewAccountQuarter2WithCumulative900M_ThrowsConflictException()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2026 // New account in current year
        };

        var category = new BusinessCategory
        {
            BusinessCategoryId = Guid.NewGuid(),
            Code = "DIST_GOODS",
            Name = "Goods",
            VatRate = 1m,
            PitRate = 0.5m
        };

        var business = new BusinessProfile
        {
            Id = businessId,
            OwnerId = ownerId,
            Owner = owner,
            MainCategory = category
        };

        var (startQ2, endQ2) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 2);
        var periodQ2 = new TaxPeriod
        {
            Id = periodId,
            BusinessId = businessId,
            PeriodType = TaxPeriodTypes.Quarterly,
            Year = 2026,
            Quarter = 2,
            PeriodStartDate = startQ2,
            PeriodEndDate = endQ2,
            Status = TaxPeriodStatuses.Open
        };

        var periods = new Mock<ITaxPeriodRepository>();
        periods.Setup(x => x.GetByIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(periodQ2);
        periods.Setup(x => x.BusinessBelongsToUserAsync(businessId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        periods.Setup(x => x.GetBusinessWithCategoryAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);

        var policies = new Mock<ITaxPolicyService>();
        policies.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse
            {
                AnnualRevenueThreshold = 1_000_000_000m,
                SupportedRevenueCeiling = 50_000_000_000m
            });

        // Q1 = 600M + Q2 = 300M -> Annual cumulative = 900M (<= 1B)
        var ownerRevenue = new Mock<IOwnerRevenueProjector>();
        var annualProjection900M = new OwnerRevenueProjection(
            ownerId,
            new DateTime(2025, 12, 31, 17, 0, 0),
            new DateTime(2026, 12, 31, 17, 0, 0),
            CompletedTransactionRevenue: 900_000_000m,
            ManualBusinessRevenue: 0m,
            Blockers: [])
        {
            Lines = [
                new OwnerRevenueLine(category.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q1", new DateTime(2026, 2, 10, 5, 0, 0), "Q1", 600_000_000m),
                new OwnerRevenueLine(category.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q2", new DateTime(2026, 5, 10, 5, 0, 0), "Q2", 300_000_000m)
            ]
        };

        ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(ownerId, businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(annualProjection900M);

        var service = new TaxPeriodService(
            periods.Object,
            Mock.Of<ITaxCalculationRepository>(),
            policies.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IAccountingTransactionLockRepository>(),
            Mock.Of<IS2eBookProjector>(),
            Mock.Of<IInventoryMovementRepository>(),
            Mock.Of<IInventoryQuarterFinalizer>(),
            ownerRevenue.Object);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.GetCalculationPreviewAsync(ownerId, periodId));
        Assert.Contains("Doanh thu năm chưa vượt 1 tỷ đồng; hãy dùng 01/TKN-CNKD.", ex.Message);
    }
}
