using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IIngredientPurchaseService
{
    Task<IngredientPurchaseResponse> CreateAsync(Guid ownerId, Guid businessId, CreateIngredientPurchaseRequest request);
    Task<IEnumerable<IngredientPurchaseResponse>> CreateBatchAsync(Guid ownerId, Guid businessId, CreateBatchIngredientPurchaseRequest request);
    Task<IngredientPurchaseResponse> UpdateAsync(Guid ownerId, Guid id, UpdateIngredientPurchaseRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IngredientPurchaseResponse> GetByIdAsync(Guid ownerId, Guid id);
    Task<PagedResult<IngredientPurchaseResponse>> GetPagedByBusinessAsync(
        Guid ownerId, Guid businessId, int pageNumber, int pageSize, string? search);
}
