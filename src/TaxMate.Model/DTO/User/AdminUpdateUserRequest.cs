using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.User;

public class AdminUpdateUserRequest
{
    [MaxLength(200)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? TaxCode { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MinLength(8)]
    public string? Password { get; set; }

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }
}
