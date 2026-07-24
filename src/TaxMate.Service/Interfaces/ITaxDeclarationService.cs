using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.TaxDeclaration;

namespace TaxMate.Service.Interfaces;

public interface ITaxDeclarationService
{
    Task<TaxDeclarationResponse> CreateAsync(
        Guid userId,
        Guid taxPeriodId,
        CreateTaxDeclarationRequest request,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationResponse> GetByIdAsync(
        Guid userId,
        Guid declarationId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationResponse> GetByTaxPeriodAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclarationResponse> SubmitAsync(
        Guid userId,
        Guid declarationId,
        SubmitTaxDeclarationRequest request,
        CancellationToken cancellationToken = default);
    
    Task<TaxDeclarationGeneratedFile> ExportAsync(
        Guid userId,
        Guid declarationId,
        CancellationToken cancellationToken = default);
}