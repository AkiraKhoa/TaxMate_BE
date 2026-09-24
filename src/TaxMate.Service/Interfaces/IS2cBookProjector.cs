using TaxMate.Model.DTO.Expense;

namespace TaxMate.Service.Interfaces;

public interface IS2cBookProjector
{
    Task<S2cBookProjection> ProjectQuarterAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);
}
