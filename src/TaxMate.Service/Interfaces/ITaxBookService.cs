using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.DTO.Tax;

namespace TaxMate.Service.Interfaces;

public interface ITaxBookService
{
    Task<IReadOnlyList<QttOffsetObligationOption>> GetQttOffsetObligationsAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportQttAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        CancellationToken cancellationToken = default);

    Task<QttPreviewResponse> GetQttPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<QttCalculationPreviewResponse> GetQttCalculationPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<QttCalculationResponse> CalculateQttAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> CreateQttDeclarationAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> UpdateQttOverpaymentAllocationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        UpdateQttOverpaymentAllocationRequest request,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> ConfirmQttDeclarationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        ConfirmQttDeclarationRequest request,
        CancellationToken cancellationToken = default);

    Task<S2cBookProjection> GetS2cPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<S2cBookProjection> ConfirmS2cEvidenceReviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportS2cAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<OwnerRevenueProjection> GetS2bPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportS2bAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<S2eBookProjection> GetS2ePreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<S2dBook> GetS2dPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportS2dAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportS2eAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportS1aAsync(
        Guid userId,
        Guid businessId,
        int year,
        int? quarter,
        CancellationToken cancellationToken = default);
}
