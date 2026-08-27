using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IRevenueThresholdAlertService
{
    Task<IReadOnlyList<RevenueThresholdAlert>> EvaluateAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);
}
