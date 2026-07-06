using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IIncomeRepository : IGenericRepository<Income>
{
    Task<(List<Income> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod);

    Task<List<Income>> GetMonthlyIncomesAsync(Guid businessId, int year, int month);
    Task<Income?> GetByIdWithCategoryAsync(Guid id);
}
