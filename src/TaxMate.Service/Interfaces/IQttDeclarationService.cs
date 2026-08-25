using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces;

public interface IQttDeclarationService
{
    Task<IReadOnlyList<QttOffsetObligationOption>> GetOffsetObligationsAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationGeneratedFile> ExportAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> CreateAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> UpdateAllocationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        UpdateQttOverpaymentAllocationRequest request,
        CancellationToken cancellationToken = default);

    Task<QttDeclarationResponse> ConfirmAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        ConfirmQttDeclarationRequest request,
        CancellationToken cancellationToken = default);
}
