using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class IncomeCategoryRepository : GenericRepository<IncomeCategory>, IIncomeCategoryRepository
{
    private readonly AppDbContext _appContext;

    public IncomeCategoryRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<IEnumerable<IncomeCategory>> GetCategoriesForBusinessAsync(Guid businessId)
    {
        return await _appContext.IncomeCategories
            .Where(x => x.BusinessId == null || x.BusinessId == businessId)
            .OrderBy(x => x.CategoryName)
            .ToListAsync();
    }
}
