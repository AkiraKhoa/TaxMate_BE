namespace TaxMate.Model.DTO.Auth;

public class ProfileOtpResponse
{
    public string Message { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime ResendAvailableAt { get; set; }
}
