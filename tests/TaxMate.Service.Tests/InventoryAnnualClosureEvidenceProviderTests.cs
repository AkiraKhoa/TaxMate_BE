using System.Reflection;
using TaxMate.Model.Common;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class InventoryAnnualClosureEvidenceProviderTests
{
    [Fact]
    public void EvidenceAndLedgerServices_AreNotPublicApiContracts()
    {
        Assert.False(typeof(InventoryAnnualClosureEvidence).IsPublic);
        Assert.False(typeof(InventoryAnnualClosureEvidenceProvider).IsPublic);
        Assert.False(typeof(IInventoryAnnualClosureEvidenceProvider).IsPublic);
        Assert.False(typeof(InventoryValuationService).IsPublic);
        Assert.False(typeof(IInventoryValuationService).IsPublic);
        Assert.Empty(typeof(InventoryAnnualClosureEvidence).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public async Task Create_AcquiresOwnerYearLockBeforeScopeAndPeriodReads()
    {
        var log = new List<string>();
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var scope = new OwnerBusinessScope(
            ownerId,
            new HashSet<Guid> { businessId });
        var scopeRepository = new FakeAccountingScopeReadRepository
        {
            Scope = scope,
            Log = log
        };
        var lockRepository = new FakeAccountingTransactionLockRepository
        {
            HasActiveTransaction = true,
            Log = log
        };
        var closureRepository = new FakeInventoryBookClosureRepository
        {
            States = ClosedStates(businessId, 2026),
            Log = log
        };

        var evidence = await new InventoryAnnualClosureEvidenceProvider(
                scopeRepository,
                lockRepository,
                closureRepository)
            .CreateAsync(ownerId, businessId, 2026);

        Assert.Equal(new[] { "lock:2026", "scope", "periods" }, log);
        Assert.Equal(ownerId, evidence.OwnerId);
        Assert.Equal(2026, evidence.Year);
        Assert.Contains(businessId, evidence.BusinessIds);
    }

    [Fact]
    public async Task Create_RequiresActiveTransactionBeforeAnyRead()
    {
        var log = new List<string>();
        var provider = new InventoryAnnualClosureEvidenceProvider(
            new FakeAccountingScopeReadRepository { Log = log },
            new FakeAccountingTransactionLockRepository
            {
                HasActiveTransaction = false,
                Log = log
            },
            new FakeInventoryBookClosureRepository { Log = log });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), 2026));
        Assert.Empty(log);
    }

    [Fact]
    public async Task Create_RejectsMissingQuarter()
    {
        var (provider, ownerId, businessId, repository) = ValidProvider();
        repository.States = ClosedStates(businessId, 2026)
            .Where(x => x.Quarter != 4)
            .ToList();

        var exception = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            provider.CreateAsync(ownerId, businessId, 2026));

        Assert.Equal(InventoryBookBlockerCodes.MissingClosedBookQuarters, exception.ErrorCode);
    }

    [Fact]
    public async Task Create_RejectsOpenQuarterEvenWhenStaleClosedDuplicateExists()
    {
        var (provider, ownerId, businessId, repository) = ValidProvider();
        var states = ClosedStates(businessId, 2026).ToList();
        var q4 = states.Single(x => x.Quarter == 4);
        states.Add(q4 with
        {
            TaxPeriodId = Guid.NewGuid(),
            BusinessId = businessId,
            Status = TaxPeriodStatuses.Open
        });
        repository.States = states;

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            provider.CreateAsync(ownerId, businessId, 2026));
    }

    [Fact]
    public async Task Create_RejectsStaleQuarterBoundariesForRequestedYear()
    {
        var (provider, ownerId, businessId, repository) = ValidProvider();
        repository.States = ClosedStates(businessId, 2025);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            provider.CreateAsync(ownerId, businessId, 2026));
    }

    [Fact]
    public async Task Create_RejectsWrongAuthenticatedOwnerAfterLockWithoutPeriodRead()
    {
        var log = new List<string>();
        var actualOwnerId = Guid.NewGuid();
        var authenticatedOwnerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var closure = new FakeInventoryBookClosureRepository { Log = log };
        var provider = new InventoryAnnualClosureEvidenceProvider(
            new FakeAccountingScopeReadRepository
            {
                Scope = new OwnerBusinessScope(
                    actualOwnerId,
                    new HashSet<Guid> { businessId }),
                Log = log
            },
            new FakeAccountingTransactionLockRepository
            {
                HasActiveTransaction = true,
                Log = log
            },
            closure);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            provider.CreateAsync(authenticatedOwnerId, businessId, 2026));

        Assert.Equal(new[] { "lock:2026", "scope" }, log);
        Assert.Equal(0, closure.ReadCount);
    }

    [Fact]
    public async Task Create_RejectsRequestedBusinessOutsideResolvedOwnerScope()
    {
        var ownerId = Guid.NewGuid();
        var requestedBusinessId = Guid.NewGuid();
        var provider = new InventoryAnnualClosureEvidenceProvider(
            new FakeAccountingScopeReadRepository
            {
                Scope = new OwnerBusinessScope(
                    ownerId,
                    new HashSet<Guid> { Guid.NewGuid() })
            },
            new FakeAccountingTransactionLockRepository
            {
                HasActiveTransaction = true
            },
            new FakeInventoryBookClosureRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            provider.CreateAsync(ownerId, requestedBusinessId, 2026));
    }

    private static (
        InventoryAnnualClosureEvidenceProvider Provider,
        Guid OwnerId,
        Guid BusinessId,
        FakeInventoryBookClosureRepository Repository) ValidProvider()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new FakeInventoryBookClosureRepository
        {
            States = ClosedStates(businessId, 2026)
        };
        return (
            new InventoryAnnualClosureEvidenceProvider(
                new FakeAccountingScopeReadRepository
                {
                    Scope = new OwnerBusinessScope(
                        ownerId,
                        new HashSet<Guid> { businessId })
                },
                new FakeAccountingTransactionLockRepository
                {
                    HasActiveTransaction = true
                },
                repository),
            ownerId,
            businessId,
            repository);
    }

    private static IReadOnlyList<InventoryQuarterPeriodState> ClosedStates(
        Guid businessId,
        int year)
    {
        return Enumerable.Range(1, 4)
            .Select(quarter =>
            {
                var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(
                    year,
                    quarter);
                return new InventoryQuarterPeriodState(
                    Guid.NewGuid(),
                    businessId,
                    quarter,
                    start,
                    end,
                    TaxPeriodStatuses.Closed);
            })
            .ToList();
    }
}

internal sealed class FakeInventoryBookClosureRepository
    : IInventoryBookClosureRepository
{
    public IReadOnlyList<InventoryQuarterPeriodState> States { get; set; } = [];

    public List<string>? Log { get; init; }

    public int ReadCount { get; private set; }

    public Task<IReadOnlyList<InventoryQuarterPeriodState>> GetQuarterPeriodStatesAsync(
        IReadOnlyCollection<Guid> ownerBusinessIds,
        int year,
        CancellationToken cancellationToken = default)
    {
        ReadCount++;
        Log?.Add("periods");
        return Task.FromResult(States);
    }
}
