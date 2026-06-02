using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Invoice : BaseEntity
{
    [Key]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = null!;

    [MaxLength(50)]
    public string? InvoiceTemplateCode { get; set; }

    [MaxLength(50)]
    public string? Symbol { get; set; }

    public Guid BusinessId { get; set; }

    [Precision(18,2)]
    public decimal TotalAmount { get; set; }

    public DateTime IssueDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    [MaxLength(1000)]
    public string? PdfUrl { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<InvoiceDetail> InvoiceDetails { get; set; }
        = new List<InvoiceDetail>();
}