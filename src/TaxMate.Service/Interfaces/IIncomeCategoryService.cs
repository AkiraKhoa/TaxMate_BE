using TaxMate.Model.DTO.IncomeCategory;

namespace TaxMate.Service.Interfaces;

public interface IIncomeCategoryService
{
    Task<IncomeCategoryDTO> CreateAsync(Guid ownerId, Guid businessId, CreateIncomeCategoryRequest request);
    Task<IncomeCategoryDTO> UpdateAsync(Guid ownerId, Guid id, UpdateIncomeCategoryRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IEnumerable<IncomeCategoryDTO>> GetByBusinessAsync(Guid ownerId, Guid businessId);
    Task<IncomeCategoryDTO> GetByIdAsync(Guid ownerId, Guid id);
}
