using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

public class ApplyDiscountRequest
{
    [Required]
    public string DiscountType { get; set; } = null!;

    [Required]
    public decimal DiscountValue { get; set; }
}
