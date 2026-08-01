using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IProductIngredientRepository : IGenericRepository<ProductIngredient>
{
    Task<List<ProductIngredient>> GetByProductIdAsync(Guid productId);

    Task<ProductIngredient?> GetByCompositeKeyAsync(Guid productId, Guid ingredientId);

    Task<ProductIngredient?> GetLinkOnlyAsync(Guid productId, Guid ingredientId);

    Task<bool> ExistsAsync(Guid productId, Guid ingredientId);
}
