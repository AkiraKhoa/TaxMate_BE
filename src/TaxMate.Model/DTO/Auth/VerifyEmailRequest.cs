using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = null!;
}
