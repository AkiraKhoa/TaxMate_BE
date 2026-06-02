using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }

    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = null!;

    [Precision(18,2)]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
}