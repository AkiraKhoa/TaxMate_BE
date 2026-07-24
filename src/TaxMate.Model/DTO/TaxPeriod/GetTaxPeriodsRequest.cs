namespace TaxMate.Model.DTO.TaxPeriod;

public class GetTaxPeriodsRequest
{
    public int? Year { get; set; }

    public string? PeriodType { get; set; }

    public string? Status { get; set; }
}