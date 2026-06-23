namespace TaxMate.Model.DTO.ExpenseCategory;

public class ExpenseCategoryDTO
{
    public Guid ExpenseCategoryId { get; set; }
    public Guid? BusinessId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
