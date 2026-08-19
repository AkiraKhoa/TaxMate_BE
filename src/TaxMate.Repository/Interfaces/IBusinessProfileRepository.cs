using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IBusinessProfileRepository : IGenericRepository<BusinessProfile>
{
    Task<(List<BusinessProfile> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search);
    Task<BusinessProfile?> GetByIdWithCategoryAsync(Guid id);
    Task<BusinessProfile?> GetByIdWithOwnerAndCategoryAsync(Guid id);
    Task<List<BusinessProfile>> GetActiveByOwnerWithOwnerAndCategoryAsync(Guid ownerId);
}
