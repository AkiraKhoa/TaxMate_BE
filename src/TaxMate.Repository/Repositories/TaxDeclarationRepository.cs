using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class TaxDeclarationRepository : GenericRepository<TaxDeclaration>, ITaxDeclarationRepository
{
    private readonly AppDbContext _dbContext;
    public void AddObligation(TaxDeclarationObligation obligation) =>
        _dbContext.Set<TaxDeclarationObligation>().Add(obligation);

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

    public Task<TaxDeclaration?> GetCurrentByTaxPeriodAndFormAsync(
        Guid taxPeriodId,
        string formCode,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxDeclarations
            .Include(x => x.Obligations)
            .Include(x => x.TaxPeriod)
            .ThenInclude(x => x.Business)
            .Where(x =>
                x.TaxPeriodId == taxPeriodId &&
                x.FormCode == formCode &&
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

    public Task<TaxCalculation?> GetCurrentCalculationWithLinesAsync(
        Guid taxPeriodId,
        string recommendedFormCode,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxCalculations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x =>
                    x.TaxPeriodId == taxPeriodId &&
                    x.RecommendedFormCode == recommendedFormCode &&
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

    public async Task<IReadOnlyList<TaxDeclarationObligation>> GetObligationsByIdsAsync(
        IReadOnlyCollection<Guid> obligationIds,
        CancellationToken cancellationToken = default)
    {
        if (obligationIds.Count == 0)
            return Array.Empty<TaxDeclarationObligation>();

        return await _dbContext.TaxDeclarationObligations
            .AsNoTracking()
            .Include(x => x.TaxDeclaration)
            .ThenInclude(x => x.TaxPeriod)
            .ThenInclude(x => x.Business)
            .Where(x => obligationIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxDeclarationObligation>> GetOffsetObligationsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaxDeclarationObligations
            .AsNoTracking()
            .Include(x => x.TaxDeclaration)
            .ThenInclude(x => x.TaxPeriod)
            .ThenInclude(x => x.Business)
            .Where(x =>
                x.PayableAmount > 0m &&
                x.TaxDeclaration.TaxPeriod.PaidDate == null &&
                x.TaxDeclaration.IsCurrent &&
                (x.TaxDeclaration.Status == TaxDeclarationStatuses.Generated ||
                 x.TaxDeclaration.Status == TaxDeclarationStatuses.Submitted) &&
                x.TaxDeclaration.TaxPeriod.Business.OwnerId == ownerId)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.TaxDeclaration.DeclarationCode)
            .ToListAsync(cancellationToken);
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
