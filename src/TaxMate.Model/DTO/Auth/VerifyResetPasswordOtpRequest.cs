using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class VerifyResetPasswordOtpRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    public string Otp { get; set; } = null!;
}
