namespace TaxMate.Service.Interfaces;

public interface ITaxPeriodMutationGuard
{
    Task EnsureCanCreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime newOccurrenceAt,
        CancellationToken cancellationToken = default);

    Task EnsureCanMutateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime oldOccurrenceAt,
        DateTime newOccurrenceAt,
        CancellationToken cancellationToken = default);

    Task EnsureCanDeleteAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime oldOccurrenceAt,
        CancellationToken cancellationToken = default);
}
