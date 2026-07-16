using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IProductCategoryService
{
    Task<ProductCategoryResponse> CreateAsync(Guid ownerId, Guid businessId, CreateProductCategoryRequest request);
    Task<ProductCategoryResponse> UpdateAsync(Guid ownerId, Guid id, UpdateProductCategoryRequest request);
    Task DeleteAsync(Guid ownerId, Guid id, Guid? fallbackProductCategoryId = null, bool forceDelete = false);
    Task<IEnumerable<ProductCategoryResponse>> GetByBusinessAsync(Guid ownerId, Guid businessId);
    Task<ProductCategoryResponse> GetByIdAsync(Guid ownerId, Guid id);
    Task<List<ProductResponse>> GetActiveProductsUsingCategoryAsync(Guid ownerId, Guid productCategoryId);
}
