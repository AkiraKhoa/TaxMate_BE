using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class SubscriptionPlanRepository 
    : GenericRepository<SubscriptionPlan>, 
        ISubscriptionPlanRepository
{
    public SubscriptionPlanRepository(DbContext context) 
        : base(context)
    {
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _dbSet.AnyAsync(x => 
            x.Name.ToLower() == name.ToLower());
    }

    public async Task<SubscriptionPlan?> GetByIdWithFeaturesAsync(Guid id)
    {
        return await _dbSet
            .Include(x => x.PlanFeatures)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CountAsync(bool? isActive = null)
    {
        var query = _dbSet.AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return await query.CountAsync();
    }

    public async Task<List<SubscriptionPlan>> GetPagedAsync(
        int page,
        int pageSize,
        bool? isActive = null)
    {
        var query = _dbSet
            .Include(x => x.PlanFeatures)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}