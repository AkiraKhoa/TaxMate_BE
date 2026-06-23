using TaxMate.Model.DTO.ExpenseCategory;

namespace TaxMate.Service.Interfaces;

public interface IExpenseCategoryService
{
    Task<ExpenseCategoryDTO> CreateAsync(Guid ownerId, Guid businessId, CreateExpenseCategoryRequest request);
    Task<ExpenseCategoryDTO> UpdateAsync(Guid ownerId, Guid id, UpdateExpenseCategoryRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IEnumerable<ExpenseCategoryDTO>> GetByBusinessAsync(Guid ownerId, Guid businessId);
    Task<ExpenseCategoryDTO> GetByIdAsync(Guid ownerId, Guid id);
}
