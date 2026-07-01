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
    public string Provider { get; set; } = null!; // "VNPT", "FPT", "MISA", "Hilo", "Mock"

    [Required]
    [MaxLength(500)]
    public string ApiUrl { get; set; } = null!;

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    [MaxLength(200)]
    public string? Password { get; set; }

    [MaxLength(50)]
    public string? InvoiceTemplateCode { get; set; } // Mẫu số hóa đơn (e.g. 1C22TBB)

    [MaxLength(50)]
    public string? Symbol { get; set; } // Ký hiệu hóa đơn (e.g. 1/001)

    public bool IsEnabled { get; set; }

    public BusinessProfile Business { get; set; } = null!;
}
