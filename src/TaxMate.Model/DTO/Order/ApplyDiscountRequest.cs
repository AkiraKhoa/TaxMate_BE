using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu áp dụng giảm giá toàn đơn.</summary>
public class ApplyDiscountRequest
{
    /// <summary>Loại giảm giá: Percentage hoặc Fixed.</summary>
    /// <example>Percentage</example>
    [Required]
    public string DiscountType { get; set; } = null!;

    /// <summary>Giá trị giảm (% hoặc số tiền).</summary>
    /// <example>10</example>
    [Required]
    public decimal DiscountValue { get; set; }
}
