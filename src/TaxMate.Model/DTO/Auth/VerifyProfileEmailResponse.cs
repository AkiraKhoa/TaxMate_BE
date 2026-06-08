namespace TaxMate.Model.DTO.Auth;

public class VerifyProfileEmailResponse
{
    public string Message { get; set; } = null!;
    public UserDto User { get; set; } = null!;
}
