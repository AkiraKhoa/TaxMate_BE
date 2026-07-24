using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxDeclarationObligation : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxDeclarationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TaxType { get; set; } = null!;
    
    [MaxLength(50)]
    public string? BusinessLocationCode { get; set; }

    [MaxLength(500)]
    public string? StateBudgetContent { get; set; }

    [MaxLength(50)]
    public string? IndicatorCode { get; set; }

    [Precision(18, 2)]
    public decimal AssessedAmount { get; set; }

    [Precision(18, 2)]
    public decimal ExemptionAmount { get; set; }

    [Precision(18, 2)]
    public decimal PayableAmount { get; set; }

    [MaxLength(50)]
    public string? StateBudgetChapterCode { get; set; }

    [MaxLength(50)]
    public string? StateBudgetSubsectionCode { get; set; }

    [MaxLength(50)]
    public string? AdministrativeAreaCode { get; set; }

    [MaxLength(255)]
    public string? CollectingAuthority { get; set; }

    [MaxLength(255)]
    public string? TaxAuthority { get; set; }

    public DateTime? DueDate { get; set; }

    public TaxDeclaration TaxDeclaration { get; set; } = null!;
}