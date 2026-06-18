using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;

namespace TaxMate.Service.Interfaces;

public interface IExpenseService
{
    Task<ExpenseDTO> CreateAsync(Guid ownerId, Guid businessId, CreateExpenseRequest request);
    Task<ExpenseDTO> UpdateAsync(Guid ownerId, Guid id, UpdateExpenseRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<ExpenseDTO> GetByIdAsync(Guid ownerId, Guid id);
    Task<PagedResult<ExpenseDTO>> GetPagedAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod);
    Task<ExpenseSummaryDTO> GetMonthlySummaryAsync(Guid ownerId, Guid businessId, int year, int month);
}
