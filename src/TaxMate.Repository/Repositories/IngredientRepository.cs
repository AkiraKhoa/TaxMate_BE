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

    public async Task<(List<Ingredient> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search)
    {
        var query = _appContext.Ingredients
            .Where(x => !x.IsDeleted)
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
