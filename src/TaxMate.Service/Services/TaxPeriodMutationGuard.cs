using TaxMate.Model.Common;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class TaxPeriodMutationGuard : ITaxPeriodMutationGuard
{
    private readonly IAccountingScopeReadRepository _readRepository;
    private readonly IAccountingTransactionLockRepository _lockRepository;

    public TaxPeriodMutationGuard(
        IAccountingScopeReadRepository readRepository,
        IAccountingTransactionLockRepository lockRepository)
    {
        _readRepository = readRepository;
        _lockRepository = lockRepository;
    }

    public Task EnsureCanCreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime newOccurrenceAt,
        CancellationToken cancellationToken = default)
    {
        return EnsureOccurrencesAreOpenAsync(
            authenticatedOwnerId,
            businessId,
            [newOccurrenceAt],
            cancellationToken);
    }

    public Task EnsureCanMutateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime oldOccurrenceAt,
        DateTime newOccurrenceAt,
        CancellationToken cancellationToken = default)
    {
        return EnsureOccurrencesAreOpenAsync(
            authenticatedOwnerId,
            businessId,
            [oldOccurrenceAt, newOccurrenceAt],
            cancellationToken);
    }

    public Task EnsureCanDeleteAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime oldOccurrenceAt,
        CancellationToken cancellationToken = default)
    {
        return EnsureOccurrencesAreOpenAsync(
            authenticatedOwnerId,
            businessId,
            [oldOccurrenceAt],
            cancellationToken);
    }

    private async Task EnsureOccurrencesAreOpenAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        IReadOnlyCollection<DateTime> occurrences,
        CancellationToken cancellationToken)
    {
        if (!_lockRepository.HasActiveTransaction)
        {
            throw new InvalidOperationException(
                "Tax-period mutation checks require an active database transaction.");
        }

        var occurrenceInstants = occurrences
            .Select(BangkokBusinessTime.NormalizeNaiveUtc)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var years = occurrenceInstants
            .Select(BangkokBusinessTime.GetBangkokCalendarYear)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        // Serialize mutation and tax-period close for the same owner/year. The
        // caller owns the active transaction; this guard never begins/commits it.
        await _lockRepository.AcquireOwnerYearLocksAsync(
            authenticatedOwnerId,
            years,
            cancellationToken);

        // Scope and periods are intentionally read only after acquiring the
        // advisory locks, closing the check/write race with period close.
        var scope = await _readRepository.ResolveOwnerScopeAsync(
            businessId,
            cancellationToken);

        if (scope is null || !scope.BusinessIds.Contains(businessId))
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (scope.OwnerId != authenticatedOwnerId)
        {
            throw new ForbiddenException();
        }

        var queryStart = occurrenceInstants[0];
        var latestOccurrence = occurrenceInstants[^1];
        var queryEnd = latestOccurrence == DateTime.MaxValue
            ? latestOccurrence
            : latestOccurrence.AddTicks(1);

        var periods = await _readRepository.GetTaxPeriodsIntersectingAsync(
            scope.BusinessIds,
            queryStart,
            queryEnd,
            cancellationToken);

        var lockedPeriod = periods.FirstOrDefault(period =>
            !string.Equals(
                period.Status,
                TaxPeriodStatuses.Open,
                StringComparison.OrdinalIgnoreCase) &&
            occurrenceInstants.Any(occurrence =>
                BangkokBusinessTime.ContainsNaiveUtc(
                    BangkokBusinessTime.NormalizeNaiveUtc(period.StartNaiveUtc),
                    BangkokBusinessTime.NormalizeNaiveUtc(period.EndExclusiveNaiveUtc),
                    occurrence)));

        if (lockedPeriod is not null)
        {
            throw new ConflictException(
                $"Không thể thay đổi dữ liệu thuộc kỳ thuế đã khóa ({lockedPeriod.TaxPeriodId}).");
        }
    }
}
