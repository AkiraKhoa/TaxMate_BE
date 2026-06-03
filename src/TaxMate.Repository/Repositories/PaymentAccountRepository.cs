using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class PaymentAccountRepository : GenericRepository<PaymentAccount>, IPaymentAccountRepository
{
    private readonly AppDbContext _context;

    public PaymentAccountRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<PaymentAccount?> GetDefaultByBusinessIdAsync(Guid businessId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsDefault);
    }

    public async Task<IEnumerable<PaymentAccount>> GetAllByBusinessIdAsync(Guid businessId)
    {
        return await _dbSet
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task UnsetAllDefaultAsync(Guid businessId)
    {
        var defaults = await _dbSet
            .Where(x => x.BusinessId == businessId && x.IsDefault)
            .ToListAsync();

        foreach (var account in defaults)
        {
            account.IsDefault = false;
        }
    }
}
