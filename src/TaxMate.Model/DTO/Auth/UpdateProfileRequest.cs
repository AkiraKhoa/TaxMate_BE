using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Auth;

public class UpdateProfileRequest
{
    [Required]
    public string TaxCode { get; set; } = null!;

    [Required]
    public string Phone { get; set; } = null!;
}
