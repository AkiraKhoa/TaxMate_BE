using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IExpenseRepository : IGenericRepository<Expense>
{
    Task<(List<Expense> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod);

    Task<List<Expense>> GetMonthlyExpensesAsync(Guid businessId, int year, int month);
    Task<Expense?> GetByIdWithCategoryAsync(Guid id);
}
