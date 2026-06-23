namespace TaxMate.Model.DTO.Expense;

public class ExpenseSummaryDTO
{
    public decimal TotalExpense { get; set; }
    public List<ExpenseByCategoryDTO> ByCategories { get; set; } = new();
}

public class ExpenseByCategoryDTO
{
    public Guid ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
