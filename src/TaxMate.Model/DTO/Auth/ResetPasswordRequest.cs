using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string ConfirmPassword { get; set; } = null!;
}
