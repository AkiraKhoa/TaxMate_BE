using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IProductIngredientService
{
    Task<ProductIngredientResponse> AddAsync(
        Guid ownerId,
        Guid productId,
        AddProductIngredientRequest request);

    Task<ProductIngredientResponse> UpdateAsync(
        Guid ownerId,
        Guid productId,
        Guid ingredientId,
        UpdateProductIngredientRequest request);

    Task DeleteAsync(Guid ownerId, Guid productId, Guid ingredientId);

    Task<IEnumerable<ProductIngredientResponse>> GetByProductIdAsync(Guid ownerId, Guid productId);
}
