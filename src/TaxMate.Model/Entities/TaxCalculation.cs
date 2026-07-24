using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxCalculation : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = TaxCalculationStatuses.Completed;

    [MaxLength(100)]
    public string? CalculationRuleVersion { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    [Precision(18, 2)]
    public decimal TotalTaxableRevenue { get; set; }

    [Precision(18, 2)]
    public decimal TotalVatTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalPersonalIncomeTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalTaxBeforeExemption { get; set; }

    [Precision(18, 2)]
    public decimal TotalExemptionAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalTaxPayableAmount { get; set; }
    
    [Precision(18, 2)]
    public decimal AnnualRevenueAtCalculation { get; set; }

    [Precision(18, 2)]
    public decimal ApplicableRevenueThreshold { get; set; }

    [MaxLength(30)]
    public string RecommendedFormCode { get; set; } = null!;
    
    [Precision(18, 2)]
    public decimal RemainingPitDeduction { get; set; }

    public DateTime CalculatedAt { get; set; }

    public Guid? CalculatedByUserId { get; set; }

    public bool IsCurrent { get; set; } = true;

    public TaxPeriod TaxPeriod { get; set; } = null!;

    public ICollection<TaxCalculationLine> Lines { get; set; }
        = new List<TaxCalculationLine>();

    public ICollection<TaxDeclaration> TaxDeclarations { get; set; }
        = new List<TaxDeclaration>();
}