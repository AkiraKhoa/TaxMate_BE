using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class MoneyMovementServiceTests
{
    private readonly Mock<IMoneyMovementRepository> _repository = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    [Fact]
    public async Task SyncAsync_AddsMovementWithoutOwningSaveOrTransaction()
    {
        SetupOwner();
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(PaymentAccountTypes.Cash));
        _repository
            .Setup(x => x.GetBySourceForWriteAsync(
                MoneyMovementTypes.PaymentIn,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);

        var result = await CreateService().SyncAsync(Request());

        Assert.Equal(MoneyMovementWriteOutcome.Created, result.Outcome);
        _repository.Verify(x => x.AddAsync(
            It.Is<MoneyMovement>(movement =>
                movement.PaymentAccountId == _accountId &&
                movement.MovementType == MoneyMovementTypes.PaymentIn &&
                movement.Amount == 125_000m &&
                movement.DocumentNumber == "DH-001"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAsync_IsIdempotentForSameTypeAndReference()
    {
        SetupOwner();
        var request = Request();
        var existing = Existing(request);
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(PaymentAccountTypes.Cash));
        _repository
            .Setup(x => x.GetBySourceForWriteAsync(
                request.MovementType,
                request.ReferenceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().SyncAsync(request);

        Assert.Equal(MoneyMovementWriteOutcome.Unchanged, result.Outcome);
        Assert.Equal(existing.MoneyMovementId, result.MoneyMovementId);
        _repository.Verify(
            x => x.AddAsync(It.IsAny<MoneyMovement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_UpdatesTrackedMovementWhenOpenPeriodSourceChanges()
    {
        SetupOwner();
        var request = Request().WithAmount(250_000m);
        var existing = Existing(Request());
        var oldUpdatedAt = existing.UpdatedAt;
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(PaymentAccountTypes.Cash));
        _repository
            .Setup(x => x.GetBySourceForWriteAsync(
                request.MovementType,
                request.ReferenceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().SyncAsync(request);

        Assert.Equal(MoneyMovementWriteOutcome.Updated, result.Outcome);
        Assert.Equal(250_000m, existing.Amount);
        Assert.True(existing.UpdatedAt > oldUpdatedAt);
        _repository.Verify(
            x => x.AddAsync(It.IsAny<MoneyMovement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_RejectsTransferUsingCashAccount()
    {
        SetupOwner();
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(PaymentAccountTypes.Cash));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().SyncAsync(Request(paymentMethod: PaymentMethods.Transfer)));

        Assert.Contains("bank account", exception.Message, StringComparison.OrdinalIgnoreCase);
        _repository.Verify(
            x => x.GetBySourceForWriteAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_RejectsInactiveAccount()
    {
        SetupOwner();
        var account = Account(PaymentAccountTypes.Bank);
        account.IsActive = false;
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().SyncAsync(Request(paymentMethod: PaymentMethods.Transfer)));
    }

    [Fact]
    public async Task SyncAsync_RejectsBusinessOutsideOwnerContext()
    {
        _repository
            .Setup(x => x.GetBusinessOwnerIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService().SyncAsync(Request()));

        _repository.Verify(
            x => x.GetAccountForWriteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_NormalizesUtcMovementDateToNaiveUtc()
    {
        SetupOwner();
        var utc = new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Utc);
        _repository
            .Setup(x => x.GetAccountForWriteAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(PaymentAccountTypes.Cash));
        _repository
            .Setup(x => x.GetBySourceForWriteAsync(
                MoneyMovementTypes.PaymentIn,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);

        await CreateService().SyncAsync(Request().WithMovementDate(utc));

        _repository.Verify(x => x.AddAsync(
            It.Is<MoneyMovement>(movement =>
                movement.MovementDate.Kind == DateTimeKind.Unspecified &&
                movement.MovementDate == BangkokBusinessTime.NormalizeNaiveUtc(utc)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAsync_RejectsLocalMovementDateBeforeRepositoryAccess()
    {
        var local = new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Local);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().SyncAsync(Request().WithMovementDate(local)));

        _repository.Verify(
            x => x.GetBusinessOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotentWhenMovementDoesNotExist()
    {
        SetupOwner();
        _repository
            .Setup(x => x.GetBySourceForWriteAsync(
                MoneyMovementTypes.ExpenseOut,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);

        var deleted = await CreateService().DeleteAsync(
            _ownerId,
            _businessId,
            MoneyMovementTypes.ExpenseOut,
            Guid.NewGuid());

        Assert.False(deleted);
        _repository.Verify(x => x.Remove(It.IsAny<MoneyMovement>()), Times.Never);
    }

    private MoneyMovementService CreateService() => new(_repository.Object);

    private void SetupOwner()
        => _repository
            .Setup(x => x.GetBusinessOwnerIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_ownerId);

    private PaymentAccount Account(string accountType)
        => new()
        {
            PaymentAccountId = _accountId,
            BusinessId = _businessId,
            AccountType = accountType,
            IsActive = true,
            Business = new BusinessProfile
            {
                Id = _businessId,
                OwnerId = _ownerId,
                BusinessName = "Cửa hàng"
            }
        };

    private MoneyMovementWriteRequest Request(string paymentMethod = PaymentMethods.Cash)
        => new()
        {
            OwnerId = _ownerId,
            BusinessId = _businessId,
            PaymentAccountId = _accountId,
            PaymentMethod = paymentMethod,
            MovementType = MoneyMovementTypes.PaymentIn,
            Amount = 125_000m,
            MovementDate = new DateTime(2026, 1, 15),
            DocumentNumber = " DH-001 ",
            Description = " Thu tiền đơn DH-001 ",
            ReferenceId = Guid.Parse("33333333-3333-3333-3333-333333333333")
        };

    private static MoneyMovement Existing(MoneyMovementWriteRequest request)
    {
        var account = new PaymentAccount
        {
            PaymentAccountId = request.PaymentAccountId,
            BusinessId = request.BusinessId,
            AccountType = PaymentAccountTypes.Cash,
            IsActive = true,
            Business = new BusinessProfile
            {
                Id = request.BusinessId,
                OwnerId = request.OwnerId,
                BusinessName = "Cửa hàng"
            }
        };
        return new MoneyMovement
        {
            MoneyMovementId = Guid.NewGuid(),
            PaymentAccountId = request.PaymentAccountId,
            PaymentAccount = account,
            MovementType = request.MovementType,
            Amount = request.Amount,
            MovementDate = request.MovementDate,
            DocumentNumber = request.DocumentNumber.Trim(),
            Description = request.Description.Trim(),
            ReferenceId = request.ReferenceId,
            CreatedAt = new DateTime(2026, 1, 15),
            UpdatedAt = new DateTime(2026, 1, 15)
        };
    }
}

file static class MoneyMovementWriteRequestTestExtensions
{
    public static MoneyMovementWriteRequest WithAmount(
        this MoneyMovementWriteRequest request,
        decimal amount)
        => new()
        {
            OwnerId = request.OwnerId,
            BusinessId = request.BusinessId,
            PaymentAccountId = request.PaymentAccountId,
            PaymentMethod = request.PaymentMethod,
            MovementType = request.MovementType,
            Amount = amount,
            MovementDate = request.MovementDate,
            DocumentNumber = request.DocumentNumber,
            Description = request.Description,
            ReferenceId = request.ReferenceId
        };

    public static MoneyMovementWriteRequest WithMovementDate(
        this MoneyMovementWriteRequest request,
        DateTime movementDate)
        => new()
        {
            OwnerId = request.OwnerId,
            BusinessId = request.BusinessId,
            PaymentAccountId = request.PaymentAccountId,
            PaymentMethod = request.PaymentMethod,
            MovementType = request.MovementType,
            Amount = request.Amount,
            MovementDate = movementDate,
            DocumentNumber = request.DocumentNumber,
            Description = request.Description,
            ReferenceId = request.ReferenceId
        };
}
