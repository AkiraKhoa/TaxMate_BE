namespace TaxMate.Model.DTO.Reports;

public class ExpenseDistributionResponse
{
    public string CategoryName { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
}