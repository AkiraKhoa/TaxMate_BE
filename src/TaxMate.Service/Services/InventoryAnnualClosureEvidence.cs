using System.Collections.Frozen;

namespace TaxMate.Service.Services;

/// <summary>
/// Internal capability issued only after authoritative TaxPeriod reads under
/// the owner/year transaction lock. API callers cannot construct or pass this
/// type. It must be created and consumed inside the same coordinating database
/// transaction.
/// </summary>
internal sealed class InventoryAnnualClosureEvidence
{
    internal InventoryAnnualClosureEvidence(
        Guid ownerId,
        Guid requestedBusinessId,
        int year,
        IEnumerable<Guid> businessIds,
        Guid transactionId)
    {
        OwnerId = ownerId;
        RequestedBusinessId = requestedBusinessId;
        Year = year;
        BusinessIds = businessIds.ToFrozenSet();
        TransactionId = transactionId;
    }

    internal Guid OwnerId { get; }

    internal Guid RequestedBusinessId { get; }

    internal int Year { get; }

    internal IReadOnlySet<Guid> BusinessIds { get; }

    internal Guid TransactionId { get; }
}
