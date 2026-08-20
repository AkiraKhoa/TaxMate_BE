namespace TaxMate.Service.Options;

public class TaxSettings
{
    public const string SectionName = "TaxSettings";

    public decimal S2aMaxRevenueThreshold { get; set; } = 3_000_000_000m;
}
