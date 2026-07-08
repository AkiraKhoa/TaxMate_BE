using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.UserDevice;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserDeviceController : ControllerBase
{
    private readonly IUserDeviceService _userDeviceService;
    private readonly IFirebaseNotificationService _firebaseNotificationService;

    public UserDeviceController(
        IUserDeviceService userDeviceService,
        IFirebaseNotificationService firebaseNotificationService)
    {
        _userDeviceService = userDeviceService;
        _firebaseNotificationService = firebaseNotificationService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterDeviceRequest request)
    {
        await _userDeviceService
            .RegisterAsync(request);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Device registered successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpPost("test/{userId}")]
    public async Task<IActionResult> Test(Guid userId)
    {

        var device = await _userDeviceService.GetByUserIdAsync(userId);

        if (device == null)
        {
            throw new NotFoundException(
                "Device not found.");
        }

        await _firebaseNotificationService
            .SendAsync(
                device.DeviceToken,
                "TaxMate Test",
                "Push notification is working.");

        return Ok();
    }
}