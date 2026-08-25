using TaxMate.Service.Services;

namespace TaxMate.Service.Interfaces;

internal interface IInventoryAnnualClosureEvidenceProvider
{
    Task<InventoryAnnualClosureEvidence> CreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);
}
