namespace TaxMate.Model.DTO.Income;

public class IncomeDTO
{
    public Guid IncomeId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid IncomeCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IncomeTitle { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime IncomeDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public string? Note { get; set; }
    public string? FileUrl { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
