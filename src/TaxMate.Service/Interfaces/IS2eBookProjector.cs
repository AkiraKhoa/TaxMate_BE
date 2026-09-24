using TaxMate.Model.DTO.MoneyMovement;

namespace TaxMate.Service.Interfaces;

public interface IS2eBookProjector
{
    Task<S2eBookProjection> ProjectAsync(
        Guid ownerId,
        Guid businessId,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
}
