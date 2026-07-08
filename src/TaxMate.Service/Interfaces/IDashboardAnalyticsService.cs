using TaxMate.Model.DTO.Dashboard;

namespace TaxMate.Service.Interfaces;

public interface IDashboardAnalyticsService
{
    Task<MomCountMetricDto> GetActiveBusinessesAsync(CancellationToken cancellationToken = default);

    Task<MomCountMetricDto> GetPaidSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<MomRevenueMetricDto> GetMonthlyRevenueAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionTrendResponseDto> GetSubscriptionTrendAsync(CancellationToken cancellationToken = default);

    Task<ServicePackageDistributionResponseDto> GetServicePackageDistributionAsync(CancellationToken cancellationToken = default);

    Task<PackageRevenueResponseDto> GetPackageRevenueAsync(CancellationToken cancellationToken = default);

    Task<BusinessUserTrendResponseDto> GetBusinessUserTrendAsync(CancellationToken cancellationToken = default);
}
