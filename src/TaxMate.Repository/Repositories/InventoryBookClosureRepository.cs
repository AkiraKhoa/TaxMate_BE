using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class InventoryBookClosureRepository
    : IInventoryBookClosureRepository
{
    private readonly AppDbContext _dbContext;

    public InventoryBookClosureRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryQuarterPeriodState>> GetQuarterPeriodStatesAsync(
        IReadOnlyCollection<Guid> ownerBusinessIds,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (ownerBusinessIds.Count == 0)
        {
            return Array.Empty<InventoryQuarterPeriodState>();
        }

        return await _dbContext.TaxPeriods
            .AsNoTracking()
            .Where(x =>
                ownerBusinessIds.Contains(x.BusinessId) &&
                x.PeriodType == TaxPeriodTypes.Quarterly &&
                x.Year == year &&
                x.Quarter.HasValue)
            .Select(x => new InventoryQuarterPeriodState(
                x.Id,
                x.BusinessId,
                x.Quarter!.Value,
                x.PeriodStartDate,
                x.PeriodEndDate,
                x.Status))
            .ToListAsync(cancellationToken);
    }
}
