using TaxMate.Model.DTO.UserDevice;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IUserDeviceService
{
    Task RegisterAsync(
        RegisterDeviceRequest request);
    
    Task<UserDevice?> GetByUserIdAsync(Guid userId);
}