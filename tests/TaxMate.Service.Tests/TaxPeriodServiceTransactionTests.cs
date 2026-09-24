using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class TaxPeriodServiceTransactionTests
{
    [Fact]
    public async Task Close_QuarterFinalizesInventoryForEveryOwnerBusiness()
    {
        var ownerId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var firstBusiness = new BusinessProfile { Id = Guid.NewGuid(), OwnerId = ownerId };
        var secondBusiness = new BusinessProfile { Id = Guid.NewGuid(), OwnerId = ownerId };
        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var period = new TaxPeriod
        {
            Id = periodId,
            BusinessId = firstBusiness.Id,
            PeriodType = TaxPeriodTypes.Quarterly,
            Year = 2026,
            Quarter = 1,
            PeriodStartDate = start,
            PeriodEndDate = end,
            Status = TaxPeriodStatuses.Open
        };
        var periods = new Mock<ITaxPeriodRepository>();
        periods.Setup(x => x.GetIdentityAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPeriodIdentity(periodId, firstBusiness.Id, ownerId, 2026));
        periods.Setup(x => x.GetCanonicalByIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        periods.Setup(x => x.GetPreviewAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPeriodPreviewResponse
            {
                TaxPeriodId = periodId,
                BusinessId = firstBusiness.Id,
                CanClose = true,
                SalesRevenue = 100m,
                TotalRevenue = 100m,
                TaxableRevenue = 100m
            });
        periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(
                ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstBusiness, secondBusiness]);

        var movements = new Mock<IInventoryMovementRepository>();
        movements.Setup(x => x.GetBeforeForUpdateAsync(
                It.IsAny<Guid>(), end, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var valuation = new Mock<IInventoryQuarterFinalizer>();
        valuation.Setup(x => x.StageFinalizeBookPeriod(
                It.IsAny<IReadOnlyCollection<InventoryMovement>>(), start, end))
            .Returns(new InventoryPeriodValuation { IsProvisional = false });
        var s2e = new Mock<IS2eBookProjector>();
        s2e.Setup(x => x.ProjectAsync(
                ownerId,
                It.IsAny<Guid>(),
                start,
                end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid owner, Guid businessId, DateTime from, DateTime to, CancellationToken token) =>
                new S2eBookProjection { BusinessId = businessId });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        unitOfWork.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var transactionLock = new Mock<IAccountingTransactionLockRepository>();
        transactionLock.Setup(x => x.AcquireOwnerYearLocksAsync(
                ownerId, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new TaxPeriodService(
            periods.Object,
            new Mock<ITaxCalculationRepository>().Object,
            new Mock<ITaxPolicyService>().Object,
            unitOfWork.Object,
            transactionLock.Object,
            s2e.Object,
            movements.Object,
            valuation.Object);

        var result = await service.CloseAsync(
            ownerId,
            periodId,
            new CloseTaxPeriodRequest());

        Assert.Equal(TaxPeriodStatuses.Closed, result.Status);
        movements.Verify(x => x.GetBeforeForUpdateAsync(
            firstBusiness.Id, end, It.IsAny<CancellationToken>()), Times.Once);
        movements.Verify(x => x.GetBeforeForUpdateAsync(
            secondBusiness.Id, end, It.IsAny<CancellationToken>()), Times.Once);
        valuation.Verify(x => x.StageFinalizeBookPeriod(
            It.IsAny<IReadOnlyCollection<InventoryMovement>>(), start, end), Times.Exactly(2));
        unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Close_RollsBackWhenIdentityReadFails()
    {
        var periodId = Guid.NewGuid();
        var periods = new Mock<ITaxPeriodRepository>();
        periods
            .Setup(x => x.GetIdentityAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxPeriodIdentity?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork
            .Setup(x => x.RollbackTransactionAsync(CancellationToken.None))
            .Returns(Task.CompletedTask);

        var service = new TaxPeriodService(
            periods.Object,
            new Mock<ITaxCalculationRepository>().Object,
            new Mock<ITaxPolicyService>().Object,
            unitOfWork.Object,
            new Mock<IAccountingTransactionLockRepository>().Object,
            new Mock<IS2eBookProjector>().Object,
            new Mock<IInventoryMovementRepository>().Object,
            new Mock<IInventoryQuarterFinalizer>().Object);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CloseAsync(
                Guid.NewGuid(),
                periodId,
                new CloseTaxPeriodRequest(),
                cancellation.Token));

        unitOfWork.Verify(
            x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWork.Verify(
            x => x.RollbackTransactionAsync(CancellationToken.None),
            Times.Once);
        unitOfWork.Verify(
            x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Close_PreservesOriginalFailureWhenRollbackAlsoFails()
    {
        var periodId = Guid.NewGuid();
        var periods = new Mock<ITaxPeriodRepository>();
        periods
            .Setup(x => x.GetIdentityAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxPeriodIdentity?)null);
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork
            .Setup(x => x.RollbackTransactionAsync(CancellationToken.None))
            .ThrowsAsync(rollbackFailure);
        var service = new TaxPeriodService(
            periods.Object,
            new Mock<ITaxCalculationRepository>().Object,
            new Mock<ITaxPolicyService>().Object,
            unitOfWork.Object,
            new Mock<IAccountingTransactionLockRepository>().Object,
            new Mock<IS2eBookProjector>().Object,
            new Mock<IInventoryMovementRepository>().Object,
            new Mock<IInventoryQuarterFinalizer>().Object);

        var original = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CloseAsync(
                Guid.NewGuid(),
                periodId,
                new CloseTaxPeriodRequest()));

        Assert.Same(
            rollbackFailure,
            original.Data["TaxPeriodCloseRollbackException"]);
    }
}
