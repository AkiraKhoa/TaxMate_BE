namespace TaxMate.Model.DTO.Reports;

public class OwnerProfileRevenueRow
{
    public Guid BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public decimal Revenue { get; set; }
}
