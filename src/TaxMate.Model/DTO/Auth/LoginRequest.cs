using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class LoginRequest
{
    [Required]
    public string Login { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
