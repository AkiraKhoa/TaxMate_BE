using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ProductPriceRepository : GenericRepository<ProductPrice>, IProductPriceRepository
{
    private readonly AppDbContext _appContext;

    public ProductPriceRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<List<ProductPrice>> GetByProductIdAsync(Guid productId)
    {
        return await _appContext.ProductPrices
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.ApplyDate)
            .ToListAsync();
    }

    public async Task<bool> ExistsDuplicateApplyDateAsync(
        Guid productId,
        DateTime applyDate,
        Guid? excludeId = null)
    {
        var applyDateOnly = applyDate.Date;

        var query = _appContext.ProductPrices
            .Where(x => x.ProductId == productId && x.ApplyDate.Date == applyDateOnly);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
