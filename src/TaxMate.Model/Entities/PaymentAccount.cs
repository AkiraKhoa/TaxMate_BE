using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class PaymentAccount : BaseEntity
{
    public Guid PaymentAccountId { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(50)]
    public string BankShortName { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = null!;

    public bool IsDefault { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();
}
