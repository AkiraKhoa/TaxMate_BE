using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public sealed class PaymentAccountServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPaymentAccountRepository> _accounts = new();
    private readonly Mock<IGenericRepository<BusinessProfile>> _businesses = new();
    private readonly Mock<ISePayService> _sePay = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<ITaxPeriodMutationGuard> _mutationGuard = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    public PaymentAccountServiceTests()
    {
        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _businesses
            .Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(Business(_ownerId));
    }

    [Fact]
    public async Task DeactivateAsync_RejectsSystemCashAccount()
    {
        var cash = Account(PaymentAccountTypes.Cash);
        SetupOwnedAccount(cash);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().DeactivateAsync(_ownerId, cash.PaymentAccountId));

        _unitOfWork.Verify(
            x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _accounts.Verify(x => x.Remove(It.IsAny<PaymentAccount>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_PreservesHistoryAndPromotesAnotherActiveBank()
    {
        var bank = Account(PaymentAccountTypes.Bank);
        bank.IsDefault = true;
        bank.SePayBankAccountXid = "sepay-xid";
        bank.CassoAccessToken = "secret";
        var next = Account(PaymentAccountTypes.Bank);
        SetupOwnedAccount(bank);
        _accounts
            .Setup(x => x.GetDefaultByBusinessIdAsync(_businessId))
            .ReturnsAsync((PaymentAccount?)null);
        _accounts
            .Setup(x => x.GetFirstActiveBankAsync(_businessId, new[] { bank.PaymentAccountId }))
            .ReturnsAsync(next);

        await CreateService().DeactivateAsync(_ownerId, bank.PaymentAccountId);

        Assert.False(bank.IsActive);
        Assert.False(bank.IsDefault);
        Assert.Null(bank.SePayBankAccountXid);
        Assert.Null(bank.CassoAccessToken);
        Assert.True(next.IsDefault);
        _accounts.Verify(x => x.Remove(It.IsAny<PaymentAccount>()), Times.Never);
        _unitOfWork.Verify(
            x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SePayUnlink_DeactivatesAndClearsIdentifierWithoutDeletingAccount()
    {
        var bank = Account(PaymentAccountTypes.Bank);
        bank.SePayBankAccountXid = "linked-xid";
        _accounts
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaymentAccount, bool>>>() ))
            .ReturnsAsync(bank);

        await CreateService().DeleteBySePayBankAccountXidAsync("linked-xid");

        Assert.False(bank.IsActive);
        Assert.Null(bank.SePayBankAccountXid);
        _accounts.Verify(x => x.Remove(It.IsAny<PaymentAccount>()), Times.Never);
        _unitOfWork.Verify(
            x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByBusinessIdAsync_CanReturnInactiveAccountsForHistory()
    {
        var inactive = Account(PaymentAccountTypes.Bank);
        inactive.IsActive = false;
        _accounts
            .Setup(x => x.GetAllByBusinessIdAsync(_businessId, true))
            .ReturnsAsync(new[] { inactive });

        var result = (await CreateService().GetByBusinessIdAsync(
            _ownerId,
            _businessId,
            includeInactive: true)).Single();

        Assert.False(result.IsActive);
        Assert.Equal(PaymentAccountTypes.Bank, result.AccountType);
        _accounts.Verify(
            x => x.GetAllByBusinessIdAsync(_businessId, true),
            Times.Once);
    }

    [Fact]
    public async Task SetDefaultAsync_RejectsInactiveBank()
    {
        var bank = Account(PaymentAccountTypes.Bank);
        bank.IsActive = false;
        _accounts
            .Setup(x => x.GetByIdAsync(bank.PaymentAccountId))
            .ReturnsAsync(bank);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().SetDefaultAsync(
                _ownerId,
                _businessId,
                bank.PaymentAccountId));

        _accounts.Verify(
            x => x.UnsetAllDefaultAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotExposeAnotherOwnersAccount()
    {
        var bank = Account(PaymentAccountTypes.Bank);
        _accounts
            .Setup(x => x.GetByIdAsync(bank.PaymentAccountId))
            .ReturnsAsync(bank);
        _businesses
            .Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(Business(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().GetByIdAsync(_ownerId, bank.PaymentAccountId));
    }

    [Fact]
    public async Task UpdateInitialBalanceAsync_AllowsChangeWhenFinancialHistoryExistsButPeriodIsOpen()
    {
        var bank = Account(PaymentAccountTypes.Bank);
        bank.InitialBalance = 10m;
        bank.InitialBalanceDate = new DateOnly(2026, 1, 1);
        SetupOwnedAccount(bank);
        _accounts
            .Setup(x => x.HasMoneyMovementHistoryAsync(bank.PaymentAccountId))
            .ReturnsAsync(true);

        await CreateService().UpdateInitialBalanceAsync(
            _ownerId,
            bank.PaymentAccountId,
            new UpdatePaymentAccountInitialBalanceRequest
            {
                InitialBalance = 20m,
                InitialBalanceDate = new DateOnly(2026, 1, 1)
            });

        Assert.Equal(20m, bank.InitialBalance);
        _mutationGuard.Verify(x => x.EnsureCanMutateAsync(
            _ownerId,
            _businessId,
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _accounts.Verify(
            x => x.HasMoneyMovementHistoryAsync(It.IsAny<Guid>()),
            Times.Never);
        _unitOfWork.Verify(
            x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateInitialBalanceAsync_BlocksLockedPeriodBeforeWriting()
    {
        var cash = Account(PaymentAccountTypes.Cash);
        SetupOwnedAccount(cash);
        _mutationGuard
            .Setup(x => x.EnsureCanCreateAsync(
                _ownerId,
                _businessId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("locked"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().UpdateInitialBalanceAsync(
                _ownerId,
                cash.PaymentAccountId,
                new UpdatePaymentAccountInitialBalanceRequest
                {
                    InitialBalance = 0m,
                    InitialBalanceDate = new DateOnly(2026, 1, 1)
                }));

        Assert.Null(cash.InitialBalance);
        _accounts.Verify(
            x => x.HasMoneyMovementHistoryAsync(It.IsAny<Guid>()),
            Times.Never);
        _unitOfWork.Verify(
            x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_CreatesOnlyNormalizedBankAccount()
    {
        PaymentAccount? added = null;
        _accounts
            .Setup(x => x.GetBankByAccountNumberAsync(_businessId, "012345"))
            .ReturnsAsync((PaymentAccount?)null);
        _accounts
            .Setup(x => x.GetDefaultByBusinessIdAsync(_businessId))
            .ReturnsAsync((PaymentAccount?)null);
        _accounts
            .Setup(x => x.AddAsync(It.IsAny<PaymentAccount>()))
            .Callback<PaymentAccount>(x => added = x)
            .Returns(Task.CompletedTask);

        await CreateService().CreateAsync(
            _ownerId,
            _businessId,
            new CreatePaymentAccountRequest
            {
                BankShortName = " vcb ",
                BankName = " Vietcombank ",
                AccountNumber = " 012 345 ",
                AccountName = " nguyen van a "
            });

        Assert.NotNull(added);
        Assert.Equal(PaymentAccountTypes.Bank, added.AccountType);
        Assert.Equal("VCB", added.BankShortName);
        Assert.Equal("012345", added.AccountNumber);
        Assert.Equal("NGUYEN VAN A", added.AccountName);
        Assert.True(added.IsActive);
        Assert.True(added.IsDefault);
    }

    [Fact]
    public async Task EnsureCashAccountAsync_CreatesSystemOnlyCashWithoutBankFields()
    {
        PaymentAccount? added = null;
        _accounts
            .Setup(x => x.GetCashByBusinessIdAsync(_businessId))
            .ReturnsAsync((PaymentAccount?)null);
        _accounts
            .Setup(x => x.AddAsync(It.IsAny<PaymentAccount>()))
            .Callback<PaymentAccount>(x => added = x)
            .Returns(Task.CompletedTask);

        await CreateService().EnsureCashAccountAsync(_businessId);

        Assert.NotNull(added);
        Assert.Equal(PaymentAccountTypes.Cash, added.AccountType);
        Assert.True(added.IsActive);
        Assert.False(added.IsDefault);
        Assert.Null(added.BankName);
        Assert.Null(added.AccountNumber);
    }

    private void SetupOwnedAccount(PaymentAccount account)
    {
        _accounts
            .Setup(x => x.GetByIdAsync(account.PaymentAccountId))
            .ReturnsAsync(account);
    }

    private BusinessProfile Business(Guid ownerId)
        => new()
        {
            Id = _businessId,
            OwnerId = ownerId,
            BusinessName = "Test business"
        };

    private PaymentAccount Account(string accountType)
        => new()
        {
            PaymentAccountId = Guid.NewGuid(),
            BusinessId = _businessId,
            AccountType = accountType,
            BankShortName = accountType == PaymentAccountTypes.Bank ? "VCB" : null,
            BankName = accountType == PaymentAccountTypes.Bank ? "Vietcombank" : null,
            AccountNumber = accountType == PaymentAccountTypes.Bank ? "012345" : null,
            AccountName = accountType == PaymentAccountTypes.Bank ? "TEST" : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private PaymentAccountService CreateService()
        => new(
            _unitOfWork.Object,
            _accounts.Object,
            _businesses.Object,
            _sePay.Object,
            _transactions.Object,
            _mutationGuard.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<PaymentAccountService>.Instance);
}
