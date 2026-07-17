namespace TaxMate.Model.DTO.Income;

public class IncomeSummaryDTO
{
    public decimal TotalIncome { get; set; }
    public List<IncomeByCategoryDTO> ByCategories { get; set; } = new();
}

public class IncomeByCategoryDTO
{
    public Guid IncomeCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
