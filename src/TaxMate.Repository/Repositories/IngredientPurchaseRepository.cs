using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class IngredientPurchaseRepository : GenericRepository<IngredientPurchase>, IIngredientPurchaseRepository
{
    private readonly AppDbContext _appContext;

    public IngredientPurchaseRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<IngredientPurchase> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId, int pageNumber, int pageSize, string? search)
    {
        var query = _appContext.IngredientPurchases
            .Include(x => x.Ingredient)
            .Where(x => x.BusinessId == businessId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.Ingredient.Name.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IngredientPurchase?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _appContext.IngredientPurchases
            .Include(x => x.Ingredient)
            .Include(x => x.Business)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
