using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Transaction : BaseEntity
{
    public Guid TransactionId { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TransactionCode { get; set; } = null!;

    public DateTime TransactionDate { get; set; }

    [Precision(18, 2)]
    public decimal SubTotal { get; set; }

    [MaxLength(20)]
    public string? DiscountType { get; set; }

    [Precision(18, 2)]
    public decimal? DiscountValue { get; set; }

    [Precision(18, 2)]
    public decimal DiscountAmount { get; set; }

    [MaxLength(100)]
    public string? SurchargeName { get; set; }

    [MaxLength(20)]
    public string? SurchargeType { get; set; }

    [Precision(18, 2)]
    public decimal? SurchargeValue { get; set; }

    [Precision(18, 2)]
    public decimal SurchargeAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; }

    [MaxLength(50)]
    public string? InvoiceId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    [MaxLength(2000)]
    public string? Note { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public Invoice? Invoice { get; set; }

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();

    public ICollection<TransactionItem> TransactionItems { get; set; }
        = new List<TransactionItem>();
}