using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class Transaction
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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Invoice? Invoice { get; set; }

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();
}