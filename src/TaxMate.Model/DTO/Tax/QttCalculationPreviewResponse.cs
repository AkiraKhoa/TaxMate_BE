namespace TaxMate.Model.DTO.Tax;

public sealed class QttCalculationPreviewResponse
{
    public int TaxYear { get; init; }
    public string Eligibility { get; init; } = null!;
    public QttIndicators09To24 Indicators { get; init; } = null!;
    public QttInventoryTotals31To34 InventoryTotals { get; init; } = null!;
    public string ApplicableRateReason { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public DateTime DueDate { get; init; }
    public IReadOnlyList<QttPreviewIssue> Warnings { get; init; } = [];
}

public sealed record QttIndicators09To24(
    decimal Indicator09,
    decimal Indicator09a,
    decimal Indicator09b,
    decimal Indicator09c,
    decimal Indicator10,
    decimal Indicator10a,
    decimal Indicator10b,
    decimal Indicator10c,
    decimal Indicator10d,
    decimal Indicator10LoanInterest,
    decimal Indicator10e,
    decimal Indicator11,
    decimal Indicator12Rate,
    decimal Indicator13,
    decimal Indicator14,
    decimal Indicator15,
    decimal Indicator16,
    decimal Indicator17,
    decimal Indicator18,
    decimal Indicator19,
    decimal Indicator20,
    decimal Indicator21,
    decimal Indicator22,
    decimal Indicator23,
    decimal Indicator24);

public sealed record QttInventoryTotals31To34(
    decimal Indicator31,
    decimal Indicator32,
    decimal Indicator33,
    decimal Indicator34);

public static class QttCalculationOutcomes
{
    public const string Payable = "Payable";
    public const string Overpaid = "Overpaid";
    public const string Zero = "Zero";
}

public sealed class QttCalculationResponse
{
    public Guid TaxPeriodId { get; init; }
    public Guid CalculationId { get; init; }
    public int Version { get; init; }
    public QttCalculationPreviewResponse Calculation { get; init; } = null!;
}

public sealed class QttCalculationSnapshot
{
    public string SchemaVersion { get; init; } = null!;
    public string LegalVersion { get; init; } = null!;
    public string TemplateVersion { get; init; } = null!;
    public Guid OwnerId { get; init; }
    public int TaxYear { get; init; }
    public string TaxMethod { get; init; } = null!;
    public int? TaxMethodEffectiveYear { get; init; }
    public QttPreviewResponse Aggregate { get; init; } = null!;
    public QttCalculationPreviewResponse Calculation { get; init; } = null!;
    public IReadOnlyDictionary<string, string> SourceAggregateVersions { get; init; }
        = new Dictionary<string, string>();
    public DateTime CalculatedAt { get; init; }
}
