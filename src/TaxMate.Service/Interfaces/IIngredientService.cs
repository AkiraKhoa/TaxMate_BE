using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IIngredientService
{
    Task<IngredientResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateIngredientRequest request);

    Task<IngredientResponse> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateIngredientRequest request);

    Task DeactivateAsync(Guid ownerId, Guid id);

    Task<PagedResult<IngredientResponse>> GetPagedByBusinessAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search);

    Task<IngredientResponse> GetByIdAsync(Guid ownerId, Guid id);
}
