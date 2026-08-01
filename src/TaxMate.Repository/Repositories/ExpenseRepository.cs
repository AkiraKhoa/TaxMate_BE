using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ExpenseRepository : GenericRepository<Expense>, IExpenseRepository
{
    private readonly AppDbContext _appContext;

    public ExpenseRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<Expense> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod)
    {
        var query = _appContext.Expenses
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Supplier)
            .Where(x => x.BusinessId == businessId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.ExpenseTitle.ToLower().Contains(searchLower));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.ExpenseDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.ExpenseDate <= toDate.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.ExpenseCategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            query = query.Where(x => x.PaymentMethod == paymentMethod);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.ExpenseDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Expense>> GetMonthlyExpensesAsync(Guid businessId, int year, int month)
    {
        return await _appContext.Expenses
            .Include(x => x.ExpenseCategory)
            .Where(x => x.BusinessId == businessId 
                        && x.ExpenseDate.Year == year 
                        && x.ExpenseDate.Month == month)
            .ToListAsync();
    }

    public async Task<Expense?> GetByIdWithCategoryAsync(Guid id)
    {
        return await _appContext.Expenses
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.ExpenseId == id);
    }
}
