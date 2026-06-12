using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class RegisterRequest
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string TaxCode { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = null!;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;
}
