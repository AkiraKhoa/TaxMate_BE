using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxDeclarationLine : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxDeclarationId { get; set; }

    [Required]
    [MaxLength(20)]
    public string SectionCode { get; set; } = null!;

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

    public TaxDeclaration TaxDeclaration { get; set; } = null!;
}