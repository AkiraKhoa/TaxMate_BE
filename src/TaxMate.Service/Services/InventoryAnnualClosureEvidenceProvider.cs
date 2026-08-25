using TaxMate.Model.Common;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryAnnualClosureEvidenceProvider
    : IInventoryAnnualClosureEvidenceProvider
{
    private readonly IAccountingScopeReadRepository _scopeRepository;
    private readonly IAccountingTransactionLockRepository _lockRepository;
    private readonly IInventoryBookClosureRepository _closureRepository;

    public InventoryAnnualClosureEvidenceProvider(
        IAccountingScopeReadRepository scopeRepository,
        IAccountingTransactionLockRepository lockRepository,
        IInventoryBookClosureRepository closureRepository)
    {
        _scopeRepository = scopeRepository;
        _lockRepository = lockRepository;
        _closureRepository = closureRepository;
    }

    public async Task<InventoryAnnualClosureEvidence> CreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (authenticatedOwnerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Authenticated owner id cannot be empty.",
                nameof(authenticatedOwnerId));
        }

        if (businessId == Guid.Empty)
        {
            throw new ArgumentException(
                "Business id cannot be empty.",
                nameof(businessId));
        }

        if (!_lockRepository.HasActiveTransaction)
        {
            throw new InvalidOperationException(
                "Annual S2d closure evidence requires an active database transaction.");
        }

        // Match tax-period mutation/close serialization. No TaxPeriod read is
        // allowed before this owner/year lock is held.
        await _lockRepository.AcquireOwnerYearLocksAsync(
            authenticatedOwnerId,
            [year],
            cancellationToken);

        var transactionId = _lockRepository.CurrentTransactionId;
        if (!transactionId.HasValue)
        {
            throw new InvalidOperationException(
                "The accounting transaction ended while annual S2d closure evidence was being created.");
        }

        var scope = await _scopeRepository.ResolveOwnerScopeAsync(
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

        var states = await _closureRepository.GetQuarterPeriodStatesAsync(
            scope.BusinessIds,
            year,
            cancellationToken);
        ValidateAuthoritativeQuarterStates(scope, year, states);

        return new InventoryAnnualClosureEvidence(
            scope.OwnerId,
            businessId,
            year,
            scope.BusinessIds,
            transactionId.Value);
    }

    private static void ValidateAuthoritativeQuarterStates(
        OwnerBusinessScope scope,
        int year,
        IReadOnlyCollection<InventoryQuarterPeriodState> states)
    {
        if (states.Any(x => !scope.BusinessIds.Contains(x.BusinessId)))
        {
            throw InvalidEvidence(
                "Dữ liệu kỳ S2d chứa cửa hàng ngoài phạm vi của chủ hộ.");
        }

        var open = states.FirstOrDefault(x => string.Equals(
            x.Status,
            TaxPeriodStatuses.Open,
            StringComparison.OrdinalIgnoreCase));
        if (open is not null)
        {
            throw InvalidEvidence(
                $"Quý {open.Quarter} S2d vẫn đang mở.");
        }

        foreach (var state in states)
        {
            if (state.Quarter is < 1 or > 4)
            {
                throw InvalidEvidence("Kỳ S2d có số quý không hợp lệ.");
            }

            var expected = BangkokBusinessTime.GetQuarterNaiveUtc(
                year,
                state.Quarter);
            var actualStart = BangkokBusinessTime.NormalizeNaiveUtc(
                state.StartNaiveUtc);
            var actualEnd = BangkokBusinessTime.NormalizeNaiveUtc(
                state.EndExclusiveNaiveUtc);
            if (actualStart != expected.StartNaiveUtc ||
                actualEnd != expected.EndExclusiveNaiveUtc)
            {
                throw InvalidEvidence(
                    $"Biên thời gian quý {state.Quarter} S2d không còn khớp năm {year}.");
            }
        }

        var nonOpenQuarters = states
            .Where(x => !string.Equals(
                x.Status,
                TaxPeriodStatuses.Open,
                StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Quarter)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        if (!nonOpenQuarters.SequenceEqual(new[] { 1, 2, 3, 4 }))
        {
            throw InvalidEvidence(
                "Chỉ được tổng hợp quyết toán sau khi cả 4 quý S2d đã chốt.");
        }
    }

    private static UnprocessableEntityException InvalidEvidence(string message) =>
        new(InventoryBookBlockerCodes.MissingClosedBookQuarters, message);
}
