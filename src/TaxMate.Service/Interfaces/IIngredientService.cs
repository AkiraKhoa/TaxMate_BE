using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IIngredientService
{
    Task<IngredientResponse> CreateAsync(CreateIngredientRequest request);
    Task<IngredientResponse> UpdateAsync(Guid id, UpdateIngredientRequest request);
    Task DeactivateAsync(Guid id);
    Task<PagedResult<IngredientResponse>> GetPagedAsync(
        int pageNumber, int pageSize, string? search);
    Task<IngredientResponse> GetByIdAsync(Guid id);
}
