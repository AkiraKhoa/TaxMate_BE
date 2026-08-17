using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ProductCategoryRepository : GenericRepository<ProductCategory>, IProductCategoryRepository
{
    private readonly AppDbContext _appContext;

    public ProductCategoryRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<IEnumerable<ProductCategory>> GetByBusinessAsync(Guid businessId)
    {
        return await _appContext.ProductCategories
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetCountByBusinessAsync(Guid businessId)
    {
        return await _appContext.ProductCategories
            .CountAsync(x => x.BusinessId == businessId);
    }
}
