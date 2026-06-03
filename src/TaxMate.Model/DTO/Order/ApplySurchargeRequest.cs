using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

public class ApplySurchargeRequest
{
    [Required]
    public string SurchargeName { get; set; } = null!;

    [Required]
    public string SurchargeType { get; set; } = null!;

    [Required]
    public decimal SurchargeValue { get; set; }
}
