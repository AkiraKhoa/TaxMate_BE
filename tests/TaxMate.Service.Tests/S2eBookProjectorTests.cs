using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class S2eBookProjectorTests
{
    private readonly Mock<IMoneyMovementRepository> _repository = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _cashId = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 4, 1);
    private static readonly DateTime To = new(2026, 7, 1);

    [Fact]
    public void BalanceCalculator_UsesHalfOpenRangeAndCarriesPriorMovements()
    {
        var calculation = S2eBalanceCalculator.Calculate(
            100m,
            new DateTime(2026, 1, 1),
            From,
            To,
            [
                Movement(MoneyMovementTypes.PaymentIn, 20m, new DateTime(2026, 3, 31)),
                Movement(MoneyMovementTypes.ExpenseOut, 5m, From),
                Movement(MoneyMovementTypes.PaymentIn, 40m, To)
            ]);

        Assert.Equal(120m, calculation.OpeningBalance);
        Assert.Equal(0m, calculation.TotalIn);
        Assert.Equal(5m, calculation.TotalOut);
        Assert.Equal(115m, calculation.EndingBalance);
    }

    [Fact]
    public void BalanceCalculator_RejectsLocalMovementDate()
    {
        var localMovement = Movement(
            MoneyMovementTypes.PaymentIn,
            20m,
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Local));

        Assert.Throws<ArgumentException>(() => S2eBalanceCalculator.Calculate(
            0m,
            new DateTime(2026, 1, 1),
            From,
            To,
            [localMovement]));
    }

    [Fact]
    public async Task ProjectAsync_ReturnsBalanceEntriesAndInactiveHistory()
    {
        var bankId = Guid.NewGuid();
        SetupCommon(
            [
                Account(_cashId, PaymentAccountTypes.Cash, 100m, new DateOnly(2026, 1, 1)),
                Account(bankId, PaymentAccountTypes.Bank, 50m, new DateOnly(2026, 1, 1), false)
            ],
            [
                Movement(MoneyMovementTypes.PaymentIn, 20m, new DateTime(2026, 3, 31), _cashId),
                Movement(MoneyMovementTypes.ManualIncomeIn, 30m, From, _cashId),
                Movement(MoneyMovementTypes.ExpenseOut, 10m, new DateTime(2026, 5, 1), bankId)
            ]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.True(result.IsReady);
        Assert.Equal(170m, result.OpeningBalance);
        Assert.Equal(30m, result.TotalIn);
        Assert.Equal(10m, result.TotalOut);
        Assert.Equal(190m, result.EndingBalance);
        Assert.Equal(2, result.Accounts.Count);
        Assert.Contains(result.Accounts, x => !x.IsActive && x.TotalOut == 10m);
    }

    [Fact]
    public async Task ProjectAsync_BlocksNullInitialBalanceInsteadOfTreatingItAsZero()
    {
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, null, null)],
            []);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.False(result.IsReady);
        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.InitialBalanceUnconfirmed &&
            x.PaymentAccountId == _cashId);
    }

    [Fact]
    public async Task ProjectAsync_IgnoresBankAccountCreatedAfterReportPeriod()
    {
        var futureBank = Account(
            Guid.NewGuid(),
            PaymentAccountTypes.Bank,
            null,
            null);
        futureBank.CreatedAt = new DateTime(2026, 7, 15);
        SetupCommon(
            [
                Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1)),
                futureBank
            ],
            []);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.True(result.IsReady);
        Assert.Single(result.Accounts);
        Assert.Equal(_cashId, result.Accounts[0].PaymentAccountId);
    }

    [Fact]
    public async Task ProjectAsync_BlocksPaidSourceWithoutMovement()
    {
        var paymentId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [],
            [new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = paymentId,
                Amount = 500m,
                MovementDate = new DateTime(2026, 5, 1),
                PaymentAccountId = _cashId
            }]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.MissingSourceMovement &&
            x.ReferenceId == paymentId);
    }

    [Fact]
    public async Task ProjectAsync_BlocksMissingMovementBeforeCurrentPeriod()
    {
        var paymentId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [],
            [new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = paymentId,
                Amount = 500m,
                MovementDate = new DateTime(2026, 2, 1),
                PaymentAccountId = _cashId
            }]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.MissingSourceMovement &&
            x.ReferenceId == paymentId);
    }

    [Fact]
    public async Task ProjectAsync_DoesNotAuditSourcesBeforeAccountCutover()
    {
        var legacyPaymentId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [],
            [new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = legacyPaymentId,
                Amount = 500m,
                MovementDate = new DateTime(2025, 12, 1),
                PaymentAccountId = _cashId
            }]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.True(result.IsReady);
        Assert.DoesNotContain(result.Blockers, x =>
            x.ReferenceId == legacyPaymentId);
    }

    [Fact]
    public async Task ProjectAsync_BlocksDuplicateMovementBeforeCurrentPeriod()
    {
        var paymentId = Guid.NewGuid();
        var movementDate = new DateTime(2026, 2, 1);
        var first = Movement(
            MoneyMovementTypes.PaymentIn,
            500m,
            movementDate,
            _cashId,
            paymentId);
        var duplicate = Movement(
            MoneyMovementTypes.PaymentIn,
            500m,
            movementDate,
            _cashId,
            paymentId);
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [first, duplicate],
            [new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = paymentId,
                Amount = 500m,
                MovementDate = movementDate,
                PaymentAccountId = _cashId
            }]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.DuplicateMovementSource &&
            x.ReferenceId == paymentId);
    }

    [Fact]
    public async Task ProjectAsync_BlocksOrphanActualMovementBeforeCurrentPeriod()
    {
        var paymentId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [Movement(
                MoneyMovementTypes.PaymentIn,
                500m,
                new DateTime(2026, 2, 1),
                _cashId,
                paymentId)],
            []);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.OrphanMovementSource &&
            x.ReferenceId == paymentId);
    }

    [Fact]
    public async Task ProjectAsync_BlocksManualMovementForAutoIncomeToPreventDoubleCount()
    {
        var incomeId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [Movement(
                MoneyMovementTypes.ManualIncomeIn,
                1_000m,
                new DateTime(2026, 2, 1),
                _cashId,
                incomeId)],
            [],
            systemIncomeIds: new HashSet<Guid> { incomeId });

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.AutoIncomeDuplicateMovement &&
            x.ReferenceId == incomeId);
    }

    [Fact]
    public async Task ProjectAsync_AuditsUnboundedHistoryWhenInitialBalanceIsMissing()
    {
        var expenseId = Guid.NewGuid();
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, null, null)],
            [Movement(
                MoneyMovementTypes.ExpenseOut,
                100m,
                new DateTime(2025, 1, 1),
                _cashId,
                expenseId)],
            []);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            From,
            To);

        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.InitialBalanceUnconfirmed);
        Assert.Contains(result.Blockers, x =>
            x.Code == S2eValidationBlockerCodes.OrphanMovementSource &&
            x.ReferenceId == expenseId);
    }

    [Fact]
    public async Task ProjectAsync_IncludesFirstSevenBangkokHoursOnInitialDate()
    {
        var (from, to) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var firstHour = BangkokBusinessTime.BangkokWallClockToNaiveUtc(
            new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified));
        var paymentId = Guid.NewGuid();
        var movement = Movement(
            MoneyMovementTypes.PaymentIn,
            250m,
            firstHour,
            _cashId,
            paymentId);
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            [movement],
            [new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = paymentId,
                Amount = 250m,
                MovementDate = firstHour,
                PaymentAccountId = _cashId
            }]);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            from,
            to);

        Assert.True(result.IsReady);
        Assert.Equal(250m, result.TotalIn);
        Assert.Single(result.Accounts[0].Entries);
    }

    [Fact]
    public async Task ProjectAsync_RejectsLocalRangeBeforeRepositoryAccess()
    {
        var localFrom = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Local);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateProjector().ProjectAsync(
                _ownerId,
                _businessId,
                localFrom,
                To));

        _repository.Verify(
            x => x.GetBusinessOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProjectAsync_NormalizesUtcRangeToNaiveUtc()
    {
        SetupCommon(
            [Account(_cashId, PaymentAccountTypes.Cash, 0m, new DateOnly(2026, 1, 1))],
            []);
        var utcFrom = DateTime.SpecifyKind(From, DateTimeKind.Utc);
        var utcTo = DateTime.SpecifyKind(To, DateTimeKind.Utc);

        var result = await CreateProjector().ProjectAsync(
            _ownerId,
            _businessId,
            utcFrom,
            utcTo);

        Assert.Equal(DateTimeKind.Unspecified, result.FromInclusive.Kind);
        Assert.Equal(DateTimeKind.Unspecified, result.ToExclusive.Kind);
        Assert.Equal(From, result.FromInclusive);
        Assert.Equal(To, result.ToExclusive);
    }

    private S2eBookProjector CreateProjector() => new(_repository.Object);

    private void SetupCommon(
        IReadOnlyList<PaymentAccount> accounts,
        IReadOnlyList<MoneyMovement> movements,
        IReadOnlyList<MoneyMovementSourceAuditRecord>? expectedSources = null,
        IReadOnlySet<Guid>? systemIncomeIds = null)
    {
        _repository
            .Setup(x => x.GetBusinessOwnerIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_ownerId);
        _repository
            .Setup(x => x.GetAccountsForBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
        _repository
            .Setup(x => x.GetMovementsForBusinessBeforeAsync(
                _businessId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movements);
        _repository
            .Setup(x => x.GetExpectedSourcesBeforeAsync(
                _businessId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSources ?? movements.Select(x =>
                new MoneyMovementSourceAuditRecord
                {
                    MovementType = x.MovementType,
                    ReferenceId = x.ReferenceId,
                    Amount = x.Amount,
                    MovementDate = x.MovementDate,
                    PaymentAccountId = x.MovementType == MoneyMovementTypes.PaymentIn
                        ? x.PaymentAccountId
                        : null
                }).ToList());
        _repository
            .Setup(x => x.GetSystemIncomeIdsBeforeAsync(
                _businessId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemIncomeIds ?? new HashSet<Guid>());
    }

    private PaymentAccount Account(
        Guid id,
        string type,
        decimal? initialBalance,
        DateOnly? initialBalanceDate,
        bool isActive = true)
        => new()
        {
            PaymentAccountId = id,
            BusinessId = _businessId,
            AccountType = type,
            BankShortName = type == PaymentAccountTypes.Bank ? "VCB" : null,
            BankName = type == PaymentAccountTypes.Bank ? "Vietcombank" : null,
            AccountNumber = type == PaymentAccountTypes.Bank ? "12345678" : null,
            AccountName = type == PaymentAccountTypes.Bank ? "SHOP" : null,
            InitialBalance = initialBalance,
            InitialBalanceDate = initialBalanceDate,
            IsActive = isActive
        };

    private static MoneyMovement Movement(
        string type,
        decimal amount,
        DateTime date,
        Guid? accountId = null,
        Guid? referenceId = null)
        => new()
        {
            MoneyMovementId = Guid.NewGuid(),
            PaymentAccountId = accountId ?? Guid.NewGuid(),
            MovementType = type,
            Amount = amount,
            MovementDate = date,
            DocumentNumber = "CT-001",
            Description = "Phát sinh kiểm thử",
            ReferenceId = referenceId ?? Guid.NewGuid(),
            CreatedAt = date,
            UpdatedAt = date
        };
}
