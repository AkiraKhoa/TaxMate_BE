using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxCalculationLine : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxCalculationId { get; set; }

    /// <summary>
    /// Ví dụ: I, II, III.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string SectionCode { get; set; } = null!;

    /// <summary>
    /// Ví dụ: 08a, 08b, 08c...
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string IndicatorCode { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string BusinessActivityCode { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string BusinessActivityName { get; set; } = null!;

    public Guid? BusinessLocationId { get; set; }

    [MaxLength(50)]
    public string? BusinessLocationCode { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    [Precision(18, 2)]
    public decimal VatTaxableRevenue { get; set; }

    [Precision(18, 2)]
    public decimal ZeroRatedVatRevenue { get; set; }

    [Precision(9, 4)]
    public decimal VatTaxRate { get; set; }

    [Precision(18, 2)]
    public decimal VatTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxableRevenue { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxDeductibleRevenue { get; set; }

    [Precision(9, 4)]
    public decimal PersonalIncomeTaxRate { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal VatNonTaxableRevenue { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxRevenue { get; set; }
    
    public int DisplayOrder { get; set; }

    public TaxCalculation TaxCalculation { get; set; } = null!;
    
    public Guid? BusinessCategoryId { get; set; }

    public BusinessCategory? BusinessCategory { get; set; }
}