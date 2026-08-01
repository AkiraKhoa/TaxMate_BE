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

    [MaxLength(1000)]
    public string? CassoAccessToken { get; set; }

    [MaxLength(500)]
    public string? CassoRefreshToken { get; set; }

    [MaxLength(100)]
    public string? CassoConnectedAccountId { get; set; }

    [MaxLength(100)]
    public string? SePayBankAccountXid { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();
}
