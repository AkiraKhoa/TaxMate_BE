using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu áp dụng phụ thu toàn đơn.</summary>
public class ApplySurchargeRequest
{
    /// <summary>Tên khoản phụ thu.</summary>
    /// <example>Phí giao hàng</example>
    [Required]
    public string SurchargeName { get; set; } = null!;

    /// <summary>Loại phụ thu: Percentage hoặc Fixed.</summary>
    /// <example>Fixed</example>
    [Required]
    public string SurchargeType { get; set; } = null!;

    /// <summary>Giá trị phụ thu (% hoặc số tiền).</summary>
    /// <example>5000</example>
    [Required]
    public decimal SurchargeValue { get; set; }
}
