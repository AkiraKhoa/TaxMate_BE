using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ITaxDeclarationRepository : IGenericRepository<TaxDeclaration>
{
    Task<TaxDeclaration?> GetByIdAsync(
        Guid declarationId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclaration?> GetCurrentByTaxPeriodAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxDeclaration?> GetCurrentByTaxPeriodAndFormAsync(
        Guid taxPeriodId,
        string formCode,
        CancellationToken cancellationToken = default);

    Task<TaxCalculation?> GetCurrentCalculationWithLinesAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TaxCalculation?> GetCurrentCalculationWithLinesAsync(
        Guid taxPeriodId,
        string recommendedFormCode,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxDeclarationObligation>> GetObligationsByIdsAsync(
        IReadOnlyCollection<Guid> obligationIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxDeclarationObligation>> GetOffsetObligationsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TaxDeclaration declaration,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
