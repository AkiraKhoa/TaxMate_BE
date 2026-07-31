namespace TaxMate.Model.DTO;

public class S2aHkdDocumentModel
{
    public S2aHkdHeaderModel Header { get; set; } = new();
    public List<S2aHkdCategoryGroupModel> Groups { get; set; } = [];
    public S2aHkdFooterModel Footer { get; set; } = new();
}

public class S2aHkdHeaderModel
{
    public string BusinessName { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string TaxCode { get; set; } = null!;
    public string DeclarationPeriod { get; set; } = null!;
    public string Unit { get; set; } = "Đồng";
}

public class S2aHkdCategoryGroupModel
{
    public int GroupNumber { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string CategoryCode { get; set; } = null!;
    public decimal VatRate { get; set; }
    public decimal PitRate { get; set; }
    public List<S2aHkdLineModel> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal VatTax { get; set; }
    public decimal PitTax { get; set; }
}

public class S2aHkdLineModel
{
    public string DocumentNumber { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class S2aHkdFooterModel
{
    public decimal TotalVatTax { get; set; }
    public decimal TotalPitTax { get; set; }
    public DateTime ExportDate { get; set; }
}

public class S2aHkdProductAggregate
{
    public Guid? ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public Guid? ProductBusinessCategoryId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime LastTransactionDate { get; set; }
}
