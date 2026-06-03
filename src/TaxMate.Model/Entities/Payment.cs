using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Payment : BaseEntity
{
    public Guid PaymentId { get; set; }

    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = null!;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public Guid? PaymentAccountId { get; set; }

    public DateTime? PaidAt { get; set; }

    public Transaction Transaction { get; set; } = null!;

    public PaymentAccount? PaymentAccount { get; set; }
}