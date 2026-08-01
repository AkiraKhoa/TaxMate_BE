using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class TaxDeclarationRepository : GenericRepository<TaxDeclaration>, ITaxDeclarationRepository
{
    private readonly AppDbContext _dbContext;

    public TaxDeclarationRepository(AppDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TaxDeclaration?> GetByIdAsync(
        Guid declarationId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxDeclarations
            .Include(x => x.Lines)
            .Include(x => x.Obligations)
            .Include(x => x.TaxPeriod)
            .ThenInclude(x => x.Business)
            .FirstOrDefaultAsync(
                x => x.Id == declarationId,
                cancellationToken);
    }

    public Task<TaxDeclaration?> GetCurrentByTaxPeriodAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxDeclarations
            .Include(x => x.Lines)
            .Include(x => x.Obligations)
            .Where(x =>
                x.TaxPeriodId == taxPeriodId &&
                x.IsCurrent)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TaxCalculation?> GetCurrentCalculationWithLinesAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxCalculations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x =>
                    x.TaxPeriodId == taxPeriodId &&
                    x.IsCurrent,
                cancellationToken);
    }

    public async Task<int> GetNextVersionAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var maxVersion = await _dbContext.TaxDeclarations
            .Where(x => x.TaxPeriodId == taxPeriodId)
            .MaxAsync(
                x => (int?)x.Version,
                cancellationToken);

        return (maxVersion ?? 0) + 1;
    }

    public Task AddAsync(
        TaxDeclaration declaration,
        CancellationToken cancellationToken = default)
    {
        _dbContext.TaxDeclarations.Add(declaration);

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}