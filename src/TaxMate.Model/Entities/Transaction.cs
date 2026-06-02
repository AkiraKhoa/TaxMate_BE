using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Transaction : BaseEntity
{
    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TransactionCode { get; set; } = null!;

    public DateTime TransactionDate { get; set; }

    [Precision(18,2)]
    public decimal TotalAmount { get; set; }

    [MaxLength(50)]
    public string? InvoiceId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [MaxLength(2000)]
    public string? Note { get; set; }

    public Invoice? Invoice { get; set; }

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();
}