using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Income : BaseEntity
{
    public Guid IncomeId { get; set; }

    public Guid BusinessId { get; set; }

    public Guid IncomeCategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string IncomeTitle { get; set; } = null!;

    [Precision(18,2)]
    public decimal Amount { get; set; }

    public DateTime IncomeDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? FileUrl { get; set; }
    
    public DateTime? DueDate { get; set; }

    public DateTime? ReceivedDate { get; set; }
    
    public BusinessProfile Business { get; set; } = null!;

    public IncomeCategory IncomeCategory { get; set; } = null!;
}
