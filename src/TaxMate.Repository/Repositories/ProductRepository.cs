using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    private readonly AppDbContext _appContext;

    public ProductRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<Product> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        string? status,
        Guid? productCategoryId)
    {
        var query = _appContext.Products
            .Include(x => x.ProductPrices)
            .Include(x => x.ProductCategory)
            .Include(x => x.BusinessCategory)
            .Where(x => x.BusinessId == businessId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (productCategoryId.HasValue)
        {
            query = query.Where(x => x.ProductCategoryId == productCategoryId.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Product?> GetByIdWithPricesAsync(Guid id)
    {
        return await _appContext.Products
            .Include(x => x.ProductPrices)
            .Include(x => x.ProductCategory)
            .Include(x => x.BusinessCategory)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
