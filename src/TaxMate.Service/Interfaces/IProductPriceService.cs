using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IProductPriceService
{
    Task<ProductPriceResponse> CreateAsync(Guid ownerId, Guid productId, CreateProductPriceRequest request);
    Task<ProductPriceResponse> UpdateAsync(Guid ownerId, Guid id, UpdateProductPriceRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IEnumerable<ProductPriceResponse>> GetByProductIdAsync(Guid ownerId, Guid productId);
    Task<ProductPriceResponse> GetByIdAsync(Guid ownerId, Guid id);
}
