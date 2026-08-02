using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu thêm sản phẩm vào đơn hàng.</summary>
public class AddOrderItemRequest
{
    /// <summary>Mã sản phẩm. Chạy dotnet run --project tools/SeedTestData để lấy ID thật.</summary>
    /// <example>11111111-1111-1111-1111-111111111111</example>
    [Required]
    public Guid ProductId { get; set; }

    /// <summary>Số lượng sản phẩm.</summary>
    /// <example>2</example>
    [Required]
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    /// <summary>Loại giảm giá dòng: Percentage hoặc Fixed.</summary>
    /// <example>Percentage</example>
    public string? DiscountType { get; set; }

    /// <summary>Giá trị giảm giá (% hoặc số tiền tùy DiscountType).</summary>
    /// <example>10</example>
    public decimal? DiscountValue { get; set; }

    /// <summary>Ghi chú cho dòng hàng.</summary>
    /// <example>Ghi chú món hàng</example>
    public string? Note { get; set; }
}
