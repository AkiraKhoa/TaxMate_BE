using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class SupplierRepository : GenericRepository<Supplier>, ISupplierRepository
{
    private readonly AppDbContext _appContext;

    public SupplierRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<IEnumerable<Supplier>> GetByBusinessAsync(Guid businessId)
    {
        return await _appContext.Suppliers
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<int> GetCountByBusinessAsync(Guid businessId)
    {
        return await _appContext.Suppliers
            .CountAsync(x => x.BusinessId == businessId);
    }
}
