using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public sealed record TaxPeriodIdentity(
    Guid TaxPeriodId,
    Guid BusinessId,
    Guid OwnerId,
    int Year);

public sealed record QttTaxPaymentSource(
    Guid TaxPaymentId,
    string PaymentCode,
    DateTime PaymentDate,
    decimal Amount,
    string TaxType,
    string Status,
    string? SourceTaxMethod);

public interface ITaxPeriodRepository : IGenericRepository<TaxPeriod>
{
    Task<bool> BusinessBelongsToUserAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxPeriodSummaryResponse>> GetByBusinessAsync(
        Guid businessId,
        GetTaxPeriodsRequest request,
        CancellationToken cancellationToken = default);

    Task<TaxPeriod?> GetByIdAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxPeriod?> GetCanonicalByIdAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxPeriod?> GetQuarterAsync(
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxPeriod?> GetYearAsync(
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<TaxPeriodIdentity?> GetIdentityAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxPeriodDetailResponse?> GetDetailAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);
    
    Task<TaxPeriodPreviewResponse?> GetPreviewAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<int> GetNextCalculationVersionAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task SetPreviousCalculationsAsSupersededAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<BusinessProfile?> GetBusinessWithCategoryAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetAnnualRevenueAsync(
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetAnnualRevenueBeforePeriodAsync(
        Guid businessId,
        int year,
        DateTime periodStart,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<BusinessProfile>> GetBusinessesWithCategoriesByOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QttTaxPaymentSource>> GetTaxPaymentsByOwnerAsync(
        Guid ownerId,
        DateTime startInclusive,
        DateTime endExclusive,
        CancellationToken cancellationToken = default);

    Task<string?> GetAnnualTaxMethodSnapshotAsync(
        Guid ownerId,
        int year,
        CancellationToken cancellationToken = default);

    Task<decimal> GetRevenueForBusinessInPeriodAsync(
        Guid businessId,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken = default);


    Task<decimal> GetAnnualRevenueByOwnerAsync(
        Guid ownerId,
        int year,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetAnnualRevenueBeforePeriodByOwnerAsync(
        Guid ownerId,
        int year,
        DateTime periodStart,
        CancellationToken cancellationToken = default);

}
