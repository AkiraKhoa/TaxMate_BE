namespace TaxMate.Model.Documents.Tax;

public sealed class S2cDocumentModel
{
    public string BusinessName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string BusinessLocation { get; set; } = string.Empty;
    public string RepresentativeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Quarter { get; set; }
    public DateTime ExportDate { get; set; }
    public decimal Revenue { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal DepreciationCost { get; set; }
    public decimal PurchasedServicesCost { get; set; }
    public decimal LoanInterestCost { get; set; }
    public decimal OtherDirectCost { get; set; }
    public decimal? PitRate { get; set; }
    public decimal? PitAmount { get; set; }

    public decimal TotalExpense =>
        MaterialCost + LaborCost + DepreciationCost + PurchasedServicesCost +
        LoanInterestCost + OtherDirectCost;

    public decimal NetIncome => Revenue - TotalExpense;
}
