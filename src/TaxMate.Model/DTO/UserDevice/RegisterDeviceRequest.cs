namespace TaxMate.Model.DTO.UserDevice;

public class RegisterDeviceRequest
{
    public Guid UserId { get; set; }
    
    public string DeviceToken { get; set; } = null!;

    public string Platform { get; set; } = null!;
}