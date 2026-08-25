using TaxMate.Model.Common;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class BangkokBusinessTimeTests
{
    [Fact]
    public void CalendarYear_UsesBangkokMidnight_AsNaiveUtcHalfOpenWindow()
    {
        var (start, end) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);

        Assert.Equal(NaiveUtc(2025, 12, 31, 17), start);
        Assert.Equal(NaiveUtc(2026, 12, 31, 17), end);
        Assert.Equal(DateTimeKind.Unspecified, start.Kind);
        Assert.Equal(DateTimeKind.Unspecified, end.Kind);
        Assert.True(BangkokBusinessTime.ContainsNaiveUtc(start, end, start));
        Assert.True(BangkokBusinessTime.ContainsNaiveUtc(
            start,
            end,
            end.AddTicks(-1)));
        Assert.False(BangkokBusinessTime.ContainsNaiveUtc(start, end, end));
    }

    [Fact]
    public void WallClockAndNaiveUtc_RoundTripWithoutHostTimezone()
    {
        var wallClock = new DateTime(
            2026,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified);

        var naiveUtc = BangkokBusinessTime.BangkokWallClockToNaiveUtc(wallClock);
        var roundTrip = BangkokBusinessTime.NaiveUtcToBangkokWallClock(naiveUtc);

        Assert.Equal(NaiveUtc(2025, 12, 31, 17), naiveUtc);
        Assert.Equal(wallClock, roundTrip);
        Assert.Equal(DateTimeKind.Unspecified, roundTrip.Kind);
    }

    [Fact]
    public void NormalizeNaiveUtc_StripsUtcKind_ButRejectsHostLocalKind()
    {
        var utc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var normalized = BangkokBusinessTime.NormalizeNaiveUtc(utc);

        Assert.Equal(utc.Ticks, normalized.Ticks);
        Assert.Equal(DateTimeKind.Unspecified, normalized.Kind);
        Assert.Throws<ArgumentException>(() =>
            BangkokBusinessTime.NormalizeNaiveUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Local)));
    }

    [Fact]
    public void ExplicitConversionHelpers_RejectWrongKinds()
    {
        Assert.Throws<ArgumentException>(() =>
            BangkokBusinessTime.BangkokWallClockToNaiveUtc(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Throws<ArgumentException>(() =>
            BangkokBusinessTime.NaiveUtcToBangkokWallClock(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Quarter_RejectsOutOfRangeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BangkokBusinessTime.GetQuarterNaiveUtc(2026, 0));
    }

    internal static DateTime NaiveUtc(
        int year,
        int month,
        int day,
        int hour = 0,
        int minute = 0)
    {
        return new DateTime(
            year,
            month,
            day,
            hour,
            minute,
            0,
            DateTimeKind.Unspecified);
    }
}

public class OwnerRevenueProjectorTests
{
    [Fact]
    public async Task CalendarYear_SumsAllOwnerBusinesses_AndAvoidsAutoIncomeDoubleCount()
    {
        var ownerId = Guid.NewGuid();
        var requestedBusinessId = Guid.NewGuid();
        var siblingBusinessId = Guid.NewGuid();
        var outsideBusinessId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var (start, end) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);

        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = Scope(ownerId, requestedBusinessId, siblingBusinessId),
            Transactions =
            [
                Transaction(requestedBusinessId, transactionId, start, 100m),
                Transaction(siblingBusinessId, Guid.NewGuid(), end.AddTicks(-1), 200m),
                Transaction(siblingBusinessId, Guid.NewGuid(), end, 999m),
                Transaction(outsideBusinessId, Guid.NewGuid(), start, 888m),
                Transaction(
                    requestedBusinessId,
                    Guid.NewGuid(),
                    start,
                    777m,
                    status: TransactionStatus.Draft),
                Transaction(
                    requestedBusinessId,
                    Guid.NewGuid(),
                    start,
                    666m,
                    transactionType: TransactionTypes.ServiceRevenue)
            ],
            Incomes =
            [
                Income(requestedBusinessId, start, 50m),
                Income(siblingBusinessId, end.AddTicks(-1), 75m),
                Income(requestedBusinessId, start, 100m, transactionId),
                Income(
                    requestedBusinessId,
                    start,
                    500m,
                    accountingType: IncomeAccountingTypes.NonRevenueCashIn),
                Income(requestedBusinessId, start, 400m, accountingType: null),
                Income(siblingBusinessId, end, 300m),
                Income(outsideBusinessId, start, 200m)
            ]
        };

        var result = await new OwnerRevenueProjector(repository)
            .ProjectCalendarYearAsync(ownerId, requestedBusinessId, 2026);

        Assert.Equal(ownerId, result.OwnerId);
        Assert.Equal(300m, result.CompletedTransactionRevenue);
        Assert.Equal(125m, result.ManualBusinessRevenue);
        Assert.Equal(425m, result.TotalRevenue);
        Assert.Equal(start, result.StartNaiveUtc);
        Assert.Equal(end, result.EndExclusiveNaiveUtc);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task MissingInvoice_IsBlocking_ButCompletedSaleIsStillCounted()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = Scope(ownerId, businessId),
            Transactions =
            [
                Transaction(
                    businessId,
                    transactionId,
                    start,
                    250m,
                    hasInvoice: false)
            ]
        };

        var result = await new OwnerRevenueProjector(repository)
            .ProjectCalendarYearAsync(ownerId, businessId, 2026);

        Assert.Equal(250m, result.TotalRevenue);
        var blocker = Assert.Single(result.Blockers);
        Assert.Equal(OwnerRevenueBlockerCodes.MissingInvoice, blocker.Code);
        Assert.Equal(transactionId, blocker.SourceId);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task NonPositiveManualBusinessRevenue_IsBlocked_AndNeverReducesTotal()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = Scope(ownerId, businessId),
            Incomes =
            [
                Income(businessId, start, 10m),
                Income(businessId, start, 0m),
                Income(businessId, start, -30m)
            ]
        };

        var result = await new OwnerRevenueProjector(repository)
            .ProjectCalendarYearAsync(ownerId, businessId, 2026);

        Assert.Equal(10m, result.ManualBusinessRevenue);
        Assert.Equal(10m, result.TotalRevenue);
        Assert.Equal(2, result.Blockers.Count(x =>
            x.Code == OwnerRevenueBlockerCodes.NonPositiveManualRevenue));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task OwnerMismatch_IsForbidden()
    {
        var actualOwnerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = Scope(actualOwnerId, businessId)
        };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new OwnerRevenueProjector(repository)
                .ProjectCalendarYearAsync(Guid.NewGuid(), businessId, 2026));
    }

    private static OwnerBusinessScope Scope(Guid ownerId, params Guid[] businessIds)
    {
        return new OwnerBusinessScope(ownerId, businessIds.ToHashSet());
    }

    private static RevenueTransactionSource Transaction(
        Guid businessId,
        Guid transactionId,
        DateTime completedAt,
        decimal amount,
        string status = TransactionStatus.Completed,
        string transactionType = TransactionTypes.Sale,
        bool hasInvoice = true)
    {
        return new RevenueTransactionSource(
            businessId,
            transactionId,
            transactionType,
            status,
            completedAt,
            amount,
            hasInvoice)
        {
            BusinessCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BusinessCategoryCode = "TEST",
            BusinessCategoryName = "Ngành thử nghiệm",
            VatRate = 0.03m
        };
    }

    private static RevenueIncomeSource Income(
        Guid businessId,
        DateTime incomeDate,
        decimal amount,
        Guid? transactionId = null,
        string? accountingType = IncomeAccountingTypes.BusinessRevenue)
    {
        return new RevenueIncomeSource(
            businessId,
            Guid.NewGuid(),
            transactionId,
            accountingType,
            incomeDate,
            amount)
        {
            BusinessCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BusinessCategoryCode = "TEST",
            BusinessCategoryName = "Ngành thử nghiệm",
            VatRate = 0.03m
        };
    }
}

public class TaxPeriodMutationGuardTests
{
    [Fact]
    public async Task Create_IsBlockedByLockedSiblingBusinessPeriod()
    {
        var ownerId = Guid.NewGuid();
        var requestedBusinessId = Guid.NewGuid();
        var siblingBusinessId = Guid.NewGuid();
        var occurrence = BangkokBusinessTimeTests.NaiveUtc(2026, 2, 15, 12);
        var (repository, transactionLock) = CreateRepositories(
            ownerId,
            requestedBusinessId,
            siblingBusinessId,
            new AccountingTaxPeriodSource(
                Guid.NewGuid(),
                siblingBusinessId,
                BangkokBusinessTimeTests.NaiveUtc(2026, 1, 1),
                BangkokBusinessTimeTests.NaiveUtc(2026, 4, 1),
                TaxPeriodStatuses.Closed));

        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await Assert.ThrowsAsync<ConflictException>(() =>
            guard.EnsureCanCreateAsync(
                ownerId,
                requestedBusinessId,
                occurrence));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Mutate_ChecksBothOldAndNewOccurrence(bool lockOldOccurrence)
    {
        var ownerId = Guid.NewGuid();
        var requestedBusinessId = Guid.NewGuid();
        var siblingBusinessId = Guid.NewGuid();
        var oldOccurrence = BangkokBusinessTimeTests.NaiveUtc(2026, 2, 15, 12);
        var newOccurrence = BangkokBusinessTimeTests.NaiveUtc(2026, 5, 15, 12);
        var lockedOccurrence = lockOldOccurrence ? oldOccurrence : newOccurrence;
        var (repository, transactionLock) = CreateRepositories(
            ownerId,
            requestedBusinessId,
            siblingBusinessId,
            new AccountingTaxPeriodSource(
                Guid.NewGuid(),
                siblingBusinessId,
                lockedOccurrence.AddDays(-1),
                lockedOccurrence.AddDays(1),
                TaxPeriodStatuses.Submitted));

        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await Assert.ThrowsAsync<ConflictException>(() =>
            guard.EnsureCanMutateAsync(
                ownerId,
                requestedBusinessId,
                oldOccurrence,
                newOccurrence));
    }

    [Fact]
    public async Task Guard_RequiresActiveTransaction_BeforeAnyReadOrLock()
    {
        var log = new List<string>();
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = new OwnerBusinessScope(ownerId, new HashSet<Guid> { businessId }),
            Log = log
        };
        var transactionLock = new FakeAccountingTransactionLockRepository
        {
            HasActiveTransaction = false,
            Log = log
        };
        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.EnsureCanCreateAsync(
                ownerId,
                businessId,
                BangkokBusinessTimeTests.NaiveUtc(2026, 1, 1)));

        Assert.Empty(log);
    }

    [Fact]
    public async Task DifferentYears_AreLockedInOrder_BeforeScopeAndPeriodReads()
    {
        var log = new List<string>();
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new FakeAccountingScopeReadRepository
        {
            Scope = new OwnerBusinessScope(ownerId, new HashSet<Guid> { businessId }),
            Log = log
        };
        var transactionLock = new FakeAccountingTransactionLockRepository
        {
            HasActiveTransaction = true,
            Log = log
        };
        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await guard.EnsureCanMutateAsync(
            ownerId,
            businessId,
            BangkokBusinessTimeTests.NaiveUtc(2027, 6, 1),
            BangkokBusinessTimeTests.NaiveUtc(2026, 6, 1));

        Assert.Equal(new[] { 2026, 2027 }, transactionLock.AcquiredYears);
        Assert.Equal(new[] { "lock:2026,2027", "scope", "periods" }, log);
    }

    [Fact]
    public async Task OwnerMismatch_IsForbiddenAfterTransactionLock()
    {
        var actualOwnerId = Guid.NewGuid();
        var authenticatedOwnerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var (repository, transactionLock) = CreateRepositories(
            actualOwnerId,
            businessId,
            Guid.NewGuid());
        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            guard.EnsureCanCreateAsync(
                authenticatedOwnerId,
                businessId,
                BangkokBusinessTimeTests.NaiveUtc(2026, 2, 1)));

        Assert.Equal(authenticatedOwnerId, transactionLock.AcquiredOwnerId);
    }

    [Fact]
    public async Task PeriodEnd_IsExclusive()
    {
        var ownerId = Guid.NewGuid();
        var requestedBusinessId = Guid.NewGuid();
        var siblingBusinessId = Guid.NewGuid();
        var periodEnd = BangkokBusinessTimeTests.NaiveUtc(2026, 4, 1);
        var (repository, transactionLock) = CreateRepositories(
            ownerId,
            requestedBusinessId,
            siblingBusinessId,
            new AccountingTaxPeriodSource(
                Guid.NewGuid(),
                siblingBusinessId,
                BangkokBusinessTimeTests.NaiveUtc(2026, 1, 1),
                periodEnd,
                TaxPeriodStatuses.Paid));

        var guard = new TaxPeriodMutationGuard(repository, transactionLock);

        await guard.EnsureCanCreateAsync(ownerId, requestedBusinessId, periodEnd);
    }

    private static (
        FakeAccountingScopeReadRepository Repository,
        FakeAccountingTransactionLockRepository TransactionLock)
        CreateRepositories(
            Guid ownerId,
            Guid requestedBusinessId,
            Guid siblingBusinessId,
            params AccountingTaxPeriodSource[] periods)
    {
        return (
            new FakeAccountingScopeReadRepository
            {
                Scope = new OwnerBusinessScope(
                    ownerId,
                    new HashSet<Guid> { requestedBusinessId, siblingBusinessId }),
                Periods = periods
            },
            new FakeAccountingTransactionLockRepository
            {
                HasActiveTransaction = true
            });
    }
}

public class AccountingDocumentNumberTests
{
    [Fact]
    public void FromSource_IsStableAndDoesNotDependOnRowCount()
    {
        var sourceId = Guid.Parse("91f70dfa-41a6-480d-8d66-f93b3e436962");

        var first = AccountingDocumentNumber.FromSource("exp", sourceId);
        var second = AccountingDocumentNumber.FromSource("EXP", sourceId);

        Assert.Equal("EXP-91f70dfa41a6480d8d66f93b3e436962", first);
        Assert.Equal(first, second);
        Assert.NotEqual(
            first,
            AccountingDocumentNumber.FromSource("EXP", Guid.NewGuid()));
    }
}

internal sealed class FakeAccountingScopeReadRepository : IAccountingScopeReadRepository
{
    public OwnerBusinessScope? Scope { get; init; }
    public IReadOnlyList<RevenueTransactionSource> Transactions { get; init; }
        = Array.Empty<RevenueTransactionSource>();
    public IReadOnlyList<RevenueIncomeSource> Incomes { get; init; }
        = Array.Empty<RevenueIncomeSource>();
    public IReadOnlyList<S2cExpenseSource> Expenses { get; init; }
        = Array.Empty<S2cExpenseSource>();
    public IReadOnlyList<AccountingTaxPeriodSource> Periods { get; init; }
        = Array.Empty<AccountingTaxPeriodSource>();
    public List<string>? Log { get; init; }

    public Task<OwnerBusinessScope?> ResolveOwnerScopeAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        Log?.Add("scope");
        return Task.FromResult(Scope);
    }

    public Task<IReadOnlyList<RevenueTransactionSource>> GetRevenueTransactionsAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Transactions);
    }

    public Task<IReadOnlyList<RevenueIncomeSource>> GetRevenueIncomesAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Incomes);
    }

    public Task<IReadOnlyList<S2cExpenseSource>> GetS2cExpensesAsync(
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Expenses);
    }

    public Task<IReadOnlyList<AccountingTaxPeriodSource>> GetTaxPeriodsIntersectingAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        Log?.Add("periods");
        return Task.FromResult(Periods);
    }
}

internal sealed class FakeAccountingTransactionLockRepository
    : IAccountingTransactionLockRepository
{
    public bool HasActiveTransaction { get; init; }
    public Guid? CurrentTransactionId { get; init; } = Guid.NewGuid();
    public Guid? AcquiredOwnerId { get; private set; }
    public IReadOnlyList<int> AcquiredYears { get; private set; } = Array.Empty<int>();
    public List<string>? Log { get; init; }

    public Task AcquireOwnerYearLocksAsync(
        Guid ownerId,
        IReadOnlyCollection<int> years,
        CancellationToken cancellationToken = default)
    {
        if (!HasActiveTransaction)
        {
            throw new InvalidOperationException();
        }

        AcquiredOwnerId = ownerId;
        AcquiredYears = years.Distinct().OrderBy(x => x).ToArray();
        Log?.Add($"lock:{string.Join(',', AcquiredYears)}");
        return Task.CompletedTask;
    }
}
