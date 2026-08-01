using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;
}
