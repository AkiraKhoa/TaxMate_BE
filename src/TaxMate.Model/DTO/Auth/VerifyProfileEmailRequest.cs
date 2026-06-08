using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class VerifyProfileEmailRequest
{
    [Required]
    public string Otp { get; set; } = null!;
}
