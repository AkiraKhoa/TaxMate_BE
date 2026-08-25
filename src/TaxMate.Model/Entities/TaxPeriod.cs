using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxPeriod : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(20)]
    public string PeriodType { get; set; } = TaxPeriodTypes.Quarterly;

    public int Year { get; set; }

    public int? Month { get; set; }

    public int? Quarter { get; set; }

    public DateTime PeriodStartDate { get; set; }

    public DateTime PeriodEndDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = TaxPeriodStatuses.Open;

    // Snapshot sau khi chốt kỳ
    [Precision(18, 2)]
    public decimal SalesRevenue { get; set; }

    [Precision(18, 2)]
    public decimal OtherRevenue { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    [Precision(18, 2)]
    public decimal TaxableRevenue { get; set; }

    // Tổng số thuế sau lần tính hiện hành
    [Precision(18, 2)]
    public decimal VatTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal EstimatedTax { get; set; }

    [Precision(18, 2)]
    public decimal TaxAmountDebt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? PaidDate { get; set; }

    public DateTime? EvidenceReviewedAt { get; set; }

    public Guid? EvidenceReviewedByUserId { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public User? EvidenceReviewedByUser { get; set; }

    public ICollection<TaxCalculation> TaxCalculations { get; set; }
        = new List<TaxCalculation>();

    public ICollection<TaxDeclaration> TaxDeclarations { get; set; }
        = new List<TaxDeclaration>();

    public ICollection<TaxPayment> TaxPayments { get; set; }
        = new List<TaxPayment>();
}
