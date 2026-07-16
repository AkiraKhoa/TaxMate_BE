using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu thanh toán và hoàn tất đơn hàng.</summary>
public class CheckoutRequest
{
    /// <summary>Danh sách các khoản thanh toán. Tổng amount phải &gt;= tổng đơn.</summary>
    [Required]
    public List<PaymentEntry> Payments { get; set; } = new();

    public string? BuyerTaxCode { get; set; }
    public string? BuyerCompanyName { get; set; }
    public string? BuyerAddress { get; set; }
    public string? BuyerEmail { get; set; }
}

/// <summary>Một khoản thanh toán trong checkout.</summary>
public class PaymentEntry
{
    /// <summary>Phương thức: Cash, Transfer, ...</summary>
    /// <example>Cash</example>
    [Required]
    public string PaymentMethod { get; set; } = null!;

    /// <summary>Số tiền thanh toán.</summary>
    /// <example>100000</example>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>ID tài khoản ngân hàng (tùy chọn, dùng khi Transfer).</summary>
    /// <example>22222222-2222-2222-2222-222222222222</example>
    public Guid? PaymentAccountId { get; set; }
}
