using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ProductIngredientRepository : GenericRepository<ProductIngredient>, IProductIngredientRepository
{
    private readonly AppDbContext _appContext;

    public ProductIngredientRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<List<ProductIngredient>> GetByProductIdAsync(Guid productId)
    {
        return await _appContext.ProductIngredients
            .Include(x => x.Ingredient)
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.Ingredient.Name)
            .ToListAsync();
    }

    public async Task<ProductIngredient?> GetByCompositeKeyAsync(Guid productId, Guid ingredientId)
    {
        return await _appContext.ProductIngredients
            .Include(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.IngredientId == ingredientId);
    }

    public async Task<ProductIngredient?> GetLinkOnlyAsync(Guid productId, Guid ingredientId)
    {
        return await _appContext.ProductIngredients
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.IngredientId == ingredientId);
    }

    public async Task<bool> ExistsAsync(Guid productId, Guid ingredientId)
    {
        return await _appContext.ProductIngredients
            .AnyAsync(x => x.ProductId == productId && x.IngredientId == ingredientId);
    }
}
