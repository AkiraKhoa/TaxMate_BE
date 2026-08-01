using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IExpenseCategoryRepository : IGenericRepository<ExpenseCategory>
{
    Task<IEnumerable<ExpenseCategory>> GetCategoriesForBusinessAsync(Guid businessId);
}
