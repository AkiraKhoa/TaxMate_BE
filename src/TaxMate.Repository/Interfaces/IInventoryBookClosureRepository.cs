namespace TaxMate.Repository.Interfaces;

public sealed record InventoryQuarterPeriodState(
    Guid TaxPeriodId,
    Guid BusinessId,
    int Quarter,
    DateTime StartNaiveUtc,
    DateTime EndExclusiveNaiveUtc,
    string Status);

/// <summary>
/// Authoritative read of owner-scoped quarterly TaxPeriods for S2d closure
/// evidence. The provider calls this only after acquiring the owner/year
/// accounting transaction lock.
/// </summary>
public interface IInventoryBookClosureRepository
{
    Task<IReadOnlyList<InventoryQuarterPeriodState>> GetQuarterPeriodStatesAsync(
        IReadOnlyCollection<Guid> ownerBusinessIds,
        int year,
        CancellationToken cancellationToken = default);
}
