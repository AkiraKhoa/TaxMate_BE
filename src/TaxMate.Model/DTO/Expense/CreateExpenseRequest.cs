using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Expense;

public class CreateExpenseRequest
{
    public Guid? ExpenseCategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ExpenseTitle { get; set; } = null!;

    [Required]
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    public Guid? PaymentAccountId { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? FileUrl { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public Guid? SupplierId { get; set; }
}
