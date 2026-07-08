using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IIncomeCategoryRepository : IGenericRepository<IncomeCategory>
{
    Task<IEnumerable<IncomeCategory>> GetCategoriesForBusinessAsync(Guid businessId);
}
