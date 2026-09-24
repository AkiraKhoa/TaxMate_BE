using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class PaymentAccount : BaseEntity
{
    public Guid PaymentAccountId { get; set; }

    public Guid BusinessId { get; set; }

    [MaxLength(50)]
    public string? BankShortName { get; set; }

    [MaxLength(200)]
    public string? BankName { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    [Required]
    [MaxLength(20)]
    public string AccountType { get; set; } = PaymentAccountTypes.Bank;

    [Precision(20, 2)]
    public decimal? InitialBalance { get; set; }

    public DateOnly? InitialBalanceDate { get; set; }

    public bool IsActive { get; set; } = true;

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

    public ICollection<MoneyMovement> MoneyMovements { get; set; }
        = new List<MoneyMovement>();
}
