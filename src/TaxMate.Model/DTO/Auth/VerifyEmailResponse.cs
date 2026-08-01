namespace TaxMate.Model.DTO.Auth;

public class VerifyEmailResponse
{
    public string Message { get; set; } = null!;
    public UserDto User { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
