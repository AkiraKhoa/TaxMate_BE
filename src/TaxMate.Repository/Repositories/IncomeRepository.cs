using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class IncomeRepository : GenericRepository<Income>, IIncomeRepository
{
    private readonly AppDbContext _appContext;

    public IncomeRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<Income> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod)
    {
        var query = _appContext.Incomes
            .Include(x => x.IncomeCategory)
            .Where(x => x.BusinessId == businessId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.IncomeTitle.ToLower().Contains(searchLower));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.IncomeDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.IncomeDate <= toDate.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.IncomeCategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            query = query.Where(x => x.PaymentMethod == paymentMethod);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.IncomeDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Income>> GetMonthlyIncomesAsync(Guid businessId, int year, int month)
    {
        return await _appContext.Incomes
            .Include(x => x.IncomeCategory)
            .Where(x => x.BusinessId == businessId 
                        && x.IncomeDate.Year == year 
                        && x.IncomeDate.Month == month)
            .ToListAsync();
    }

    public async Task<Income?> GetByIdWithCategoryAsync(Guid id)
    {
        return await _appContext.Incomes
            .Include(x => x.IncomeCategory)
            .FirstOrDefaultAsync(x => x.IncomeId == id);
    }
}
