using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(x => x.TransactionItems)
            .Include(x => x.Payments)
                .ThenInclude(p => p.PaymentAccount)
            .Include(x => x.Invoice)
            .Include(x => x.Business)
            .FirstOrDefaultAsync(x => x.TransactionId == id);
    }

    public async Task<IEnumerable<Transaction>> GetByBusinessIdAsync(Guid businessId, int page, int pageSize)
    {
        return await _dbSet
            .Include(x => x.TransactionItems)
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBusinessIdAsync(Guid businessId)
    {
        return await _dbSet.CountAsync(x => x.BusinessId == businessId);
    }

    public async Task<string> GenerateTransactionCodeAsync(Guid businessId)
    {
        var localToday = DateTime.UtcNow.AddHours(7);
        var dateStr = localToday.ToString("yyyyMMdd");
        var prefix = $"DH-{dateStr}-";
        
        var count = await _dbSet.CountAsync(x => x.BusinessId == businessId && x.TransactionCode.StartsWith(prefix));
        var sequence = count + 1;
        return $"{prefix}{sequence:D3}";
    }
}
