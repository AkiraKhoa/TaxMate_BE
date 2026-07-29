using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<(List<(User User, int BusinessProfileCount)> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? role,
        string? accountStatus,
        Guid? excludeUserId = null);

    Task<User?> GetByIdWithBusinessProfilesAsync(Guid id);
}
