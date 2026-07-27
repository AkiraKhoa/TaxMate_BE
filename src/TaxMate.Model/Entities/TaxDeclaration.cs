using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxDeclaration : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    public Guid TaxCalculationId { get; set; }

    [Required]
    [MaxLength(30)]
    public string FormCode { get; set; } = "01/CNKD";

    [Required]
    [MaxLength(50)]
    public string DeclarationCode { get; set; } = null!;

    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(30)]
    public string DeclarationType { get; set; }
        = TaxDeclarationTypes.Initial;

    public int? SupplementNumber { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; }
        = TaxDeclarationStatuses.Draft;

    // Thông tin người nộp thuế tại thời điểm tạo tờ khai
    [Required]
    [MaxLength(255)]
    public string TaxpayerName { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string TaxCode { get; set; } = null!;

    [MaxLength(500)]
    public string? TaxpayerAddress { get; set; }

    [MaxLength(255)]
    public string? AuthorizedDeclarerName { get; set; }

    [MaxLength(50)]
    public string? AuthorizedDeclarerTaxCode { get; set; }

    [MaxLength(255)]
    public string? TaxAgentName { get; set; }

    [MaxLength(50)]
    public string? TaxAgentTaxCode { get; set; }

    [MaxLength(100)]
    public string? TaxAgentContractNumber { get; set; }

    public DateTime? TaxAgentContractDate { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    [Precision(18, 2)]
    public decimal TotalVatTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalPersonalIncomeTaxAmount { get; set; }

    [Precision(18, 2)]
    public decimal VatExemptionAmount { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxExemptionAmount { get; set; }

    [Precision(18, 2)]
    public decimal VatPayableAmount { get; set; }

    [Precision(18, 2)]
    public decimal PersonalIncomeTaxPayableAmount { get; set; }

    [Precision(18, 2)]
    public decimal TotalTaxPayableAmount { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    [MaxLength(50)]
    public string? SubmissionMethod { get; set; }

    [MaxLength(255)]
    public string? SubmissionReference { get; set; }

    [MaxLength(1000)]
    public string? PdfFileUrl { get; set; }

    [MaxLength(1000)]
    public string? XmlFileUrl { get; set; }
    
    [Precision(18, 2)]
    public decimal RemainingPitDeduction { get; set; }

    public bool IsCurrent { get; set; } = true;

    public TaxPeriod TaxPeriod { get; set; } = null!;

    public TaxCalculation TaxCalculation { get; set; } = null!;

    public ICollection<TaxDeclarationLine> Lines { get; set; }
        = new List<TaxDeclarationLine>();

    public ICollection<TaxDeclarationObligation> Obligations { get; set; }
        = new List<TaxDeclarationObligation>();
}