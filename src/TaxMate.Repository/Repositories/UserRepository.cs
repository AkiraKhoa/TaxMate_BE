using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    private readonly AppDbContext _appContext;

    public UserRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<(User User, int BusinessProfileCount)> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? role,
        string? accountStatus,
        Guid? excludeUserId = null)
    {
        var query = _appContext.Users.AsQueryable();

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(searchLower)
                || u.Email.ToLower().Contains(searchLower)
                || (u.Phone != null && u.Phone.ToLower().Contains(searchLower))
                || (u.TaxCode != null && u.TaxCode.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(accountStatus))
        {
            query = query.Where(u => u.AccountStatus == accountStatus);
        }

        var totalCount = await query.CountAsync();

        var pageUsers = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = pageUsers.Select(u => u.Id).ToList();
        var counts = await _appContext.BusinessProfiles
            .Where(bp => userIds.Contains(bp.OwnerId))
            .GroupBy(bp => bp.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

        var items = pageUsers
            .Select(u => (u, counts.GetValueOrDefault(u.Id, 0)))
            .ToList();

        return (items, totalCount);
    }

    public async Task<User?> GetByIdWithBusinessProfilesAsync(Guid id)
    {
        return await _appContext.Users
            .Include(u => u.BusinessProfiles)
                .ThenInclude(bp => bp.MainCategory)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}
