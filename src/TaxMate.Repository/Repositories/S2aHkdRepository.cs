using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.DTO;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class S2aHkdRepository : IS2aHkdRepository
{
    private readonly AppDbContext _context;

    public S2aHkdRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<S2aHkdProductAggregate>> GetProductAggregatesAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate)
    {
        var items = await _context.TransactionItems
            .AsNoTracking()
            .Where(ti =>
                ti.Transaction.BusinessId == businessId
                && ti.Transaction.Status == "Completed"
                && ti.Transaction.TransactionDate >= startDate
                && ti.Transaction.TransactionDate < endDate)
            .Select(ti => new
            {
                ti.ProductId,
                ProductCode = ti.Product != null ? ti.Product.ProductCode : "N/A",
                ProductName = ti.Product != null ? ti.Product.Name : ti.ProductName,
                ProductBusinessCategoryId = ti.Product != null ? ti.Product.BusinessCategoryId : null,
                ti.LineTotal,
                ti.Transaction.TransactionDate
            })
            .ToListAsync();

        return items
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductCode,
                x.ProductName,
                x.ProductBusinessCategoryId
            })
            .Select(g => new S2aHkdProductAggregate
            {
                ProductId = g.Key.ProductId,
                ProductCode = g.Key.ProductCode,
                ProductName = g.Key.ProductName,
                ProductBusinessCategoryId = g.Key.ProductBusinessCategoryId,
                TotalAmount = g.Sum(x => x.LineTotal),
                LastTransactionDate = g.Max(x => x.TransactionDate)
            })
            .OrderBy(x => x.ProductCode)
            .ToList();
    }
}
