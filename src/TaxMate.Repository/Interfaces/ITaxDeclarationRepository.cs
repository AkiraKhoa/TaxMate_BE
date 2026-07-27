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

    Task<TaxCalculation?> GetCurrentCalculationWithLinesAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TaxDeclaration declaration,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}