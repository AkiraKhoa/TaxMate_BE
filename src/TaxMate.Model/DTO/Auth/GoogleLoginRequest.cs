using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = null!;
}
