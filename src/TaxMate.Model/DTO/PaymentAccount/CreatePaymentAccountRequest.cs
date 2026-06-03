using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

public class CreatePaymentAccountRequest
{
    [Required]
    [MaxLength(50)]
    public string BankShortName { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = null!;

    public bool IsDefault { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
