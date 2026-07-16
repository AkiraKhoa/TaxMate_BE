using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class SubscriptionPlanRepository : GenericRepository<SubscriptionPlan>, ISubscriptionPlanRepository
{
    private readonly AppDbContext _appContext;

    public SubscriptionPlanRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<List<SubscriptionPlan>> GetActivePlansWithFeaturesAsync()
    {
        return await _appContext.SubscriptionPlans
            .Include(x => x.PlanFeatures)
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    public async Task<SubscriptionPlan?> GetByIdWithFeaturesAsync(Guid id)
    {
        return await _appContext.SubscriptionPlans
            .Include(x => x.PlanFeatures)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}