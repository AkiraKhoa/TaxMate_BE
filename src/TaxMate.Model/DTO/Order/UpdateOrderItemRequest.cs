namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu cập nhật dòng hàng trong đơn.</summary>
public class UpdateOrderItemRequest
{
    /// <summary>Số lượng mới. Đặt &lt;= 0 để xóa dòng.</summary>
    /// <example>3</example>
    public decimal? Quantity { get; set; }

    /// <summary>Loại giảm giá: Percentage hoặc Fixed. Chuỗi rỗng để xóa giảm giá.</summary>
    /// <example>Percentage</example>
    public string? DiscountType { get; set; }

    /// <summary>Giá trị giảm giá.</summary>
    /// <example>5</example>
    public decimal? DiscountValue { get; set; }

    /// <summary>Ghi chú dòng hàng.</summary>
    /// <example>Cập nhật ghi chú</example>
    public string? Note { get; set; }
}
