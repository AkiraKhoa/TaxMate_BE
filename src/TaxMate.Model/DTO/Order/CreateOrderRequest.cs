namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu tạo đơn hàng nháp mới.</summary>
public class CreateOrderRequest
{
    /// <summary>Ghi chú đơn hàng (tùy chọn).</summary>
    /// <example>Đơn hàng test POS</example>
    public string? Note { get; set; }
}
