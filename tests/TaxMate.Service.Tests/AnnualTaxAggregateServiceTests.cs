using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class AnnualTaxAggregateServiceTests
{
    private readonly Mock<IOwnerRevenueProjector> _revenueProjector = new();
    private readonly Mock<IS2cBookProjector> _s2cProjector = new();
    private readonly Mock<IS2dBookProjector> _s2dProjector = new();
    private readonly Mock<IInventoryMovementRepository> _inventoryMovements = new();
    private readonly Mock<ITaxPeriodRepository> _taxPeriods = new();
    private readonly Mock<IGenericRepository<User>> _users = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private const int Year = 2026;

    public AnnualTaxAggregateServiceTests()
    {
        var owner = new User
        {
            Id = _ownerId,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased,
            TaxMethodEffectiveYear = Year
        };
        _users.Setup(x => x.GetByIdAsync(_ownerId))
            .ReturnsAsync(owner);

        _taxPeriods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(_ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BusinessProfile { Id = _businessId, OwnerId = _ownerId }]);

        _revenueProjector.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, DateTime.UtcNow, DateTime.UtcNow, 2_000_000_000m, 0m, []));

        _inventoryMovements.Setup(x => x.GetBeforeAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _s2cProjector.Setup(x => x.ProjectQuarterAsync(_ownerId, _businessId, Year, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new S2cBookProjection
            {
                BusinessId = _businessId,
                TotalRevenue = 500_000_000m,
                EvidenceReviewedAt = DateTime.UtcNow
            });

        _s2dProjector.Setup(x => x.ProjectQuarter(_businessId, It.IsAny<IReadOnlyList<InventoryMovement>>(), Year, It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(new S2dBook { BusinessId = _businessId });

        _taxPeriods.Setup(x => x.GetTaxPaymentsByOwnerAsync(_ownerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _taxPeriods.Setup(x => x.GetAnnualTaxMethodSnapshotAsync(_ownerId, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PersonalIncomeTaxMethods.IncomeBased);
    }

    [Fact]
    public async Task PreviewAsync_WhenQuarterIsOpen_AddsQuarterNotClosedBlocker()
    {
        // Quarter 1, 2, 3 Closed, but Quarter 4 is Open
        _taxPeriods.Setup(x => x.GetOwnerQuarterlyFilingStatesAsync(_ownerId, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 1, TaxPeriodStatuses.Closed, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 2, TaxPeriodStatuses.Calculated, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 3, TaxPeriodStatuses.Submitted, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 4, TaxPeriodStatuses.Open, false, false, false)
            ]);

        var service = new AnnualTaxAggregateService(
            _revenueProjector.Object,
            _s2cProjector.Object,
            _s2dProjector.Object,
            _inventoryMovements.Object,
            _taxPeriods.Object,
            _users.Object);

        var preview = await service.PreviewAsync(_ownerId, _businessId, Year);

        Assert.False(preview.CanClose);
        Assert.Contains(preview.HardBlockers, b => b.Code == "Quarter4NotClosed");
        Assert.DoesNotContain(preview.HardBlockers, b => b.Code == "Quarter1NotClosed");
        Assert.False(preview.Quarters.Single(x => x.Quarter == 4).Closed);
        Assert.NotNull(preview.Quarters.Single(x => x.Quarter == 4).TaxPeriodId);
    }

    [Fact]
    public async Task PreviewAsync_WhenAll4QuartersAreClosed_CanClose()
    {
        // All 4 quarters closed/calculated/submitted
        _taxPeriods.Setup(x => x.GetOwnerQuarterlyFilingStatesAsync(_ownerId, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 1, TaxPeriodStatuses.Closed, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 2, TaxPeriodStatuses.Calculated, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 3, TaxPeriodStatuses.Submitted, true, false, true),
                new OwnerQuarterlyFilingState(Guid.NewGuid(), 4, TaxPeriodStatuses.Paid, true, false, true)
            ]);

        var service = new AnnualTaxAggregateService(
            _revenueProjector.Object,
            _s2cProjector.Object,
            _s2dProjector.Object,
            _inventoryMovements.Object,
            _taxPeriods.Object,
            _users.Object);

        var preview = await service.PreviewAsync(_ownerId, _businessId, Year);

        Assert.DoesNotContain(preview.HardBlockers, b => b.Code.StartsWith("Quarter") && b.Code.EndsWith("NotClosed"));
        Assert.True(preview.CanClose);
    }

    [Fact]
    public async Task PreviewAsync_ReviewDataPreservesBusinessQuarterAndSourceAmounts()
    {
        var secondBusiness = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        _taxPeriods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(_ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new BusinessProfile { Id = _businessId, OwnerId = _ownerId, BusinessName = "Cửa hàng A" },
                new BusinessProfile { Id = secondBusiness, OwnerId = _ownerId, BusinessName = "Cửa hàng B" }
            ]);
        _taxPeriods.Setup(x => x.GetOwnerQuarterlyFilingStatesAsync(_ownerId, Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _inventoryMovements.Setup(x => x.GetBeforeAsync(secondBusiness, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _s2dProjector.Setup(x => x.ProjectQuarter(secondBusiness, It.IsAny<IReadOnlyList<InventoryMovement>>(), Year, It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(new S2dBook { BusinessId = secondBusiness });
        _s2cProjector.Setup(x => x.ProjectQuarterAsync(_ownerId, secondBusiness, Year, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new S2cBookProjection { BusinessId = secondBusiness });
        _s2cProjector.Setup(x => x.ProjectQuarterAsync(_ownerId, secondBusiness, Year, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new S2cBookProjection
            {
                BusinessId = secondBusiness,
                ReviewLines = [new S2cExpenseReviewLine(sourceId, "inventoryPurchase", "PNK-3",
                    new DateTime(2026, 8, 1), "Mua nguyên liệu", 8_000_000m, null, ["MissingInventoryPurchaseEvidence"])]
            });
        var service = new AnnualTaxAggregateService(_revenueProjector.Object, _s2cProjector.Object,
            _s2dProjector.Object, _inventoryMovements.Object, _taxPeriods.Object, _users.Object);

        var result = await service.PreviewAsync(_ownerId, _businessId, Year);

        var row = Assert.Single(result.ExpenseReviewRows);
        Assert.Equal(secondBusiness, row.BusinessId);
        Assert.Equal(3, row.Quarter);
        Assert.Equal(sourceId, row.SourceId);
        Assert.Equal(8_000_000m, row.Amount);
        Assert.Null(row.IncludedAmount); // Purchase value must never be called deductible expense.
        var period = result.EvidenceReviewPeriods.Single(x => x.BusinessId == secondBusiness && x.Quarter == 3);
        Assert.True(period.Required);
        Assert.False(period.Reviewed);
        Assert.Equal(8, result.EvidenceReviewPeriods.Count);
        Assert.Contains(result.Warnings, x => x.Code == "EvidenceReviewRequired" && x.BusinessId == secondBusiness);
    }
}
