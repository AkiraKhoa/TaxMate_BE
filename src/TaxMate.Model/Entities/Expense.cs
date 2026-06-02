using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Expense : BaseEntity
{
    public Guid ExpenseId { get; set; }

    public Guid BusinessId { get; set; }

    public Guid ExpenseCategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ExpenseTitle { get; set; } = null!;

    [Precision(18,2)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? FileUrl { get; set; }
    
    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }
    
    public BusinessProfile Business { get; set; } = null!;

    public ExpenseCategory ExpenseCategory { get; set; } = null!;
}