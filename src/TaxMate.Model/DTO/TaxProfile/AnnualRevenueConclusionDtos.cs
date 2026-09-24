namespace TaxMate.Model.DTO.TaxProfile;

public sealed record AnnualRevenueConclusionIssue(string Code, string Message);

public sealed record AnnualRevenueConclusionQuarter(
    int Quarter,
    Guid? TaxPeriodId,
    string? PeriodStatus,
    bool IsReady);

public sealed class AnnualRevenueConclusionPreviewResponse
{
    public Guid BusinessId { get; init; }
    public int TaxYear { get; init; }
    public decimal AnnualRevenue { get; init; }
    public decimal RevenueThreshold { get; init; }
    public bool ShouldShow { get; init; }
    public bool CanConfirm { get; init; }
    public bool AlreadyConfirmed { get; init; }
    public string? CurrentRevenueBracket { get; init; }
    public string? CurrentTaxMethod { get; init; }
    public string TargetRevenueBracket { get; init; } = null!;
    public string? RequiredTaxMethod { get; init; }
    public IReadOnlyList<string> AllowedTaxMethods { get; init; } = [];
    public int AppliesFromYear { get; init; }
    public IReadOnlyList<AnnualRevenueConclusionQuarter> Quarters { get; init; } = [];
    public IReadOnlyList<AnnualRevenueConclusionIssue> BlockingIssues { get; init; } = [];
}

public sealed record ConfirmAnnualRevenueConclusionRequest(
    bool Confirmed,
    string? PersonalIncomeTaxMethod = null);
