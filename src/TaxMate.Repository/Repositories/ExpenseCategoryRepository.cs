using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ExpenseCategoryRepository : GenericRepository<ExpenseCategory>, IExpenseCategoryRepository
{
    private readonly AppDbContext _appContext;

    public ExpenseCategoryRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<IEnumerable<ExpenseCategory>> GetCategoriesForBusinessAsync(Guid businessId)
    {
        return await _appContext.ExpenseCategories
            .Where(x => x.BusinessId == null || x.BusinessId == businessId)
            .OrderBy(x => x.CategoryName)
            .ToListAsync();
    }
}
