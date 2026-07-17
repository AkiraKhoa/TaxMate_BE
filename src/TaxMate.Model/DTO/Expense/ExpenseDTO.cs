namespace TaxMate.Model.DTO.Expense;

public class ExpenseDTO
{
    public Guid ExpenseId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ExpenseTitle { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public string? Note { get; set; }
    public string? FileUrl { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
