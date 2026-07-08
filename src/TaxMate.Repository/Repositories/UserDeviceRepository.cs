using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class UserDeviceRepository 
    : GenericRepository<UserDevice>, IUserDeviceRepository
{
    public UserDeviceRepository(DbContext context) 
        : base(context)
    {
    }

    public async Task<UserDevice?> GetByTokenAsync(string token)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x => x.DeviceToken == token);
    }

    public async Task<List<UserDevice>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastActiveAt)
            .ToListAsync();
    }
}