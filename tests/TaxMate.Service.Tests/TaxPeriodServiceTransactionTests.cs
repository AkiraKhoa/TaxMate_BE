using Moq;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class TaxPeriodServiceTransactionTests
{
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
            new Mock<IS2eBookProjector>().Object);

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
            new Mock<IS2eBookProjector>().Object);

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
