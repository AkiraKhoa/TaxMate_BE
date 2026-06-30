using AutoMapper;
using TaxMate.Model.DTO.UserDevice;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class UserDeviceService : IUserDeviceService
{
    private readonly IMapper _mapper;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    public UserDeviceService(IMapper mapper, IUserDeviceRepository userDeviceRepository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _userDeviceRepository = userDeviceRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task RegisterAsync(
        RegisterDeviceRequest request)
    {
        var existing =
            await _userDeviceRepository
                .GetByTokenAsync(
                    request.DeviceToken);

        if (existing != null)
        {
            existing.LastActiveAt =
                DateTime.UtcNow;

            _userDeviceRepository
                .Update(existing);

            await _unitOfWork.SaveChangesAsync();

            return;
        }

        var device = new UserDevice
        {
            Id = Guid.NewGuid(),

            LastActiveAt = DateTime.UtcNow
        };

        await _userDeviceRepository
            .AddAsync(device);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<UserDevice?> GetByUserIdAsync(Guid userId)
    {
        return (await _userDeviceRepository
            .GetByUserIdAsync(userId)).ToList().FirstOrDefault();
    }
}