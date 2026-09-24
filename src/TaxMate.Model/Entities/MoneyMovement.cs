using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class MoneyMovement : BaseEntity
{
    public Guid MoneyMovementId { get; set; }

    public Guid PaymentAccountId { get; set; }

    [Required]
    [MaxLength(30)]
    public string MovementType { get; set; } = null!;

    [Precision(20, 2)]
    public decimal Amount { get; set; }

    public DateTime MovementDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = null!;

    public Guid ReferenceId { get; set; }

    public PaymentAccount PaymentAccount { get; set; } = null!;
}
