using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu tạo tài khoản thanh toán ngân hàng.</summary>
public class CreatePaymentAccountRequest
{
    /// <summary>Mã ngân hàng viết tắt.</summary>
    /// <example>VCB</example>
    [Required]
    [MaxLength(50)]
    public string BankShortName { get; set; } = null!;

    /// <summary>Tên đầy đủ ngân hàng.</summary>
    /// <example>Ngân hàng TMCP Ngoại thương Việt Nam</example>
    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = null!;

    /// <summary>Số tài khoản.</summary>
    /// <example>0123456789</example>
    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = null!;

    /// <summary>Tên chủ tài khoản.</summary>
    /// <example>NGUYEN VAN A</example>
    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = null!;

    /// <summary>Đặt làm tài khoản mặc định.</summary>
    /// <example>true</example>
    public bool IsDefault { get; set; }

    /// <summary>Mô tả thêm (tùy chọn).</summary>
    /// <example>Tài khoản nhận tiền chính</example>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Số dư tại ngày bắt đầu theo dõi S2e. Phải đi cùng InitialBalanceDate.</summary>
    public decimal? InitialBalance { get; set; }

    /// <summary>Ngày số dư đầu kỳ có hiệu lực. Phải đi cùng InitialBalance.</summary>
    public DateOnly? InitialBalanceDate { get; set; }
}
