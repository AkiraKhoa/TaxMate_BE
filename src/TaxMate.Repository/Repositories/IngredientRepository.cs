using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class IngredientRepository : GenericRepository<Ingredient>, IIngredientRepository
{
    private readonly AppDbContext _appContext;

    public IngredientRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<Ingredient?> GetByIdAndBusinessAsync(Guid id, Guid businessId)
    {
        return await _appContext.Ingredients
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
    }

    public async Task<(List<Ingredient> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search)
    {
        var query = _appContext.Ingredients
            .Where(x => x.BusinessId == businessId && !x.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
