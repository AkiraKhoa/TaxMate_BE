namespace TaxMate.Model.DTO.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
    public bool RequiresEmailVerification { get; set; }
}
