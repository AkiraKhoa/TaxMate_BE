using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(Guid ownerId, Guid businessId, CreateProductRequest request);
    Task<ProductResponse> UpdateAsync(Guid ownerId, Guid id, UpdateProductRequest request);
    Task<ProductResponse> ToggleStatusAsync(Guid ownerId, Guid id);
    Task<PagedResult<ProductResponse>> GetPagedByBusinessAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        string? status,
        Guid? productCategoryId);
    Task<ProductResponse> GetByIdAsync(Guid ownerId, Guid id);
}
