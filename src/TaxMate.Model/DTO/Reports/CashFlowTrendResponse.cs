public class CashFlowTrendResponse
{
    public int Month { get; set; }
    public string Label { get; set; } = null!;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}