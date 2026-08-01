using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class BusinessProfileRepository : GenericRepository<BusinessProfile>, IBusinessProfileRepository
{
    private readonly AppDbContext _appContext;

    public BusinessProfileRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<(List<BusinessProfile> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search)
    {
        var query = _appContext.BusinessProfiles
            .Include(x => x.MainCategory)
            .Where(x => x.OwnerId == ownerId && x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.BusinessName.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<BusinessProfile?> GetByIdWithCategoryAsync(Guid id)
    {
        return await _appContext.BusinessProfiles
            .Include(x => x.MainCategory)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<BusinessProfile?> GetByIdWithOwnerAndCategoryAsync(Guid id)
    {
        return await _appContext.BusinessProfiles
            .Include(x => x.Owner)
            .Include(x => x.MainCategory)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
