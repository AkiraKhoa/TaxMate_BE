using Moq;
using TaxMate.Model.DTO.Tax;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class QttServiceReadinessTests
{
    [Fact]
    public async Task CalculateAsync_WhenAnnualPreviewCanClose_CreatesCalculation()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var aggregate = new QttPreviewResponse
        {
            OwnerId = ownerId,
            TaxYear = 2026,
            TaxMethodSnapshot = "IncomeBased",
            TaxMethodEffectiveYear = 2026,
            Eligibility = "NormalIncomeBased",
            TaxpayerName = "Service shop",
            Revenue = new(120_000_000m, 0m, 0m),
            Expenses = new(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            PitPayments = new(),
            Inventory = new()
        };
        var calculated = new QttCalculationPreviewResponse
        {
            TaxYear = 2026,
            Eligibility = "NormalIncomeBased",
            Indicators = new(120_000_000m, 120_000_000m, 0m, 0m,
                0m, 0m, 0m, 0m, 0m, 0m, 0m,
                120_000_000m, 0.1m, 12_000_000m, 0m, 0m,
                12_000_000m, 0m, 12_000_000m, 12_000_000m, 0m, 0m, 0m, 0m, 0m),
            InventoryTotals = new(0m, 0m, 0m, 0m),
            ApplicableRateReason = "test",
            Outcome = QttCalculationOutcomes.Payable,
            DueDate = new DateTime(2027, 3, 31),
            CanClose = true
        };
        var annual = new Mock<IAnnualTaxAggregateService>();
        var engine = new Mock<IQttCalculationEngine>();
        var periods = new Mock<ITaxPeriodRepository>();
        var calculations = new Mock<ITaxCalculationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var transactionLock = new Mock<IAccountingTransactionLockRepository>();

        Assert.True(aggregate.CanClose);
        periods.Setup(x => x.BusinessBelongsToUserAsync(businessId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        periods.Setup(x => x.GetNextCalculationVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        annual.Setup(x => x.PreviewAsync(ownerId, businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);
        engine.Setup(x => x.Calculate(aggregate)).Returns(calculated);
        var service = new QttCalculationService(
            annual.Object, engine.Object, periods.Object, calculations.Object,
            unitOfWork.Object, transactionLock.Object);

        var result = await service.CalculateAsync(ownerId, businessId, 2026);

        Assert.Equal(calculated, result.Calculation);
        periods.Verify(x => x.AddAsync(It.IsAny<TaxMate.Model.Entities.TaxPeriod>()), Times.Once);
        calculations.Verify(x => x.AddAsync(It.IsAny<TaxMate.Model.Entities.TaxCalculation>()), Times.Once);
        unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
