using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class EInvoiceConfig : BaseEntity
{
    [Key]
    [ForeignKey(nameof(Business))]
    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = "SePay";

    [Required]
    [MaxLength(500)]
    public string BaseUrl { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string ClientId { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string ClientSecret { get; set; } = null!;

    [MaxLength(100)]
    public string? ProviderAccountId { get; set; }

    [MaxLength(50)]
    public string? InvoiceTemplateCode { get; set; } // Mẫu số hóa đơn (e.g. 1C22TBB)

    [MaxLength(50)]
    public string? Symbol { get; set; } // Ký hiệu hóa đơn (e.g. 1/001)

    public bool IsEnabled { get; set; }

    public int QuotaWarningThreshold { get; set; } = 100;

    public BusinessProfile Business { get; set; } = null!;
}
