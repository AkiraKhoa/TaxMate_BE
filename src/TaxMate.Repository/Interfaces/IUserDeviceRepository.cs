using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IUserDeviceRepository
    : IGenericRepository<UserDevice>
{
    Task<UserDevice?> GetByTokenAsync(
        string token);

    Task<List<UserDevice>> GetByUserIdAsync(
        Guid userId);
}