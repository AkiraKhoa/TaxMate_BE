using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IIngredientPurchaseService
{
    Task<IngredientPurchaseResponse> CreateAsync(Guid businessId, CreateIngredientPurchaseRequest request);
    Task<IEnumerable<IngredientPurchaseResponse>> CreateBatchAsync(Guid businessId, CreateBatchIngredientPurchaseRequest request);
    Task<IngredientPurchaseResponse> UpdateAsync(Guid id, UpdateIngredientPurchaseRequest request);
    Task DeleteAsync(Guid id);
    Task<IngredientPurchaseResponse> GetByIdAsync(Guid id);
    Task<PagedResult<IngredientPurchaseResponse>> GetPagedByBusinessAsync(
        Guid businessId, int pageNumber, int pageSize, string? search);
}
