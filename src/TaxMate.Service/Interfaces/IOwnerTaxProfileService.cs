using TaxMate.Model.DTO.TaxProfile;

namespace TaxMate.Service.Interfaces;

public interface IOwnerTaxProfileService
{
    Task<OwnerTaxProfileResponse> GetCurrentAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<OwnerTaxProfileResponse> UpdateCurrentAsync(
        Guid userId,
        Guid businessId,
        UpdateOwnerTaxProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueThresholdReviewResponse>> GetThresholdReviewsAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<RevenueThresholdReviewResponse> ConfirmThresholdReviewAsync(
        Guid userId,
        Guid businessId,
        Guid alertId,
        ConfirmRevenueThresholdReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<RevenueThresholdReviewResponse> DismissThresholdReviewAsync(
        Guid userId,
        Guid businessId,
        Guid alertId,
        CancellationToken cancellationToken = default);

    Task<AnnualRevenueConclusionPreviewResponse> PreviewAnnualConclusionAsync(
        Guid userId,
        Guid businessId,
        int taxYear,
        CancellationToken cancellationToken = default);

    Task<AnnualRevenueConclusionPreviewResponse> ConfirmAnnualConclusionAsync(
        Guid userId,
        Guid businessId,
        int taxYear,
        ConfirmAnnualRevenueConclusionRequest request,
        CancellationToken cancellationToken = default);
}
