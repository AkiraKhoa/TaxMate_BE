using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxPayment : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    public Guid? TaxDeclarationId { get; set; }

    [Required]
    [MaxLength(30)]
    public string TaxType { get; set; } = TaxTypes.Unknown;

    [Required]
    [MaxLength(50)]
    public string PaymentCode { get; set; } = null!;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; }
        = TaxPaymentStatuses.Pending;

    [MaxLength(255)]
    public string? TransactionReference { get; set; }

    [MaxLength(50)]
    public string? StateBudgetChapterCode { get; set; }

    [MaxLength(50)]
    public string? StateBudgetSubsectionCode { get; set; }

    [MaxLength(50)]
    public string? AdministrativeAreaCode { get; set; }

    [MaxLength(1000)]
    public string? ReceiptFileUrl { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public TaxPeriod TaxPeriod { get; set; } = null!;

    public TaxDeclaration? TaxDeclaration { get; set; }
}
