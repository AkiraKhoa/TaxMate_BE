namespace TaxMate.Service.Interfaces;

public interface IRevenueThresholdAlertService
{
    Task CheckAfterSaleAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);
}
