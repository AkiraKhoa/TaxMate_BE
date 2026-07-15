namespace TaxMate.Model.DTO.Reports;

public class CashFlowSummaryResponse
{
    public decimal NetAmount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
}