using TaxMate.Model.DTO.Tax;

namespace TaxMate.Model.DTO.TaxPeriod;

public sealed record TknTaxPeriodPreviewResponse(Guid TaxPeriodId, int Year,
    DateTime WindowStart, DateTime WindowEnd, DateTime? DueDate,
    decimal TotalRevenue, int RevenueGroupCount, bool CanClose,
    IReadOnlyList<string> Warnings);

public sealed record CloseTknTaxPeriodRequest(bool ConfirmWarnings);

public sealed record CloseTknTaxPeriodResponse(Guid TaxPeriodId, string Status,
    decimal TotalRevenue, DateTime ClosedAt);

public sealed record TknTaxCalculationResponse(Guid TaxPeriodId,
    Guid TaxCalculationId, int Version, decimal TotalRevenue,
    decimal ApplicableRevenueThreshold, string RecommendedFormCode,
    DateTime CalculatedAt);

public static class TknQttBridgeChoices
{
    public const string Later = "Later";
    public const string Refund = "Refund";
    public const string Offset = "Offset";

    public static readonly IReadOnlyCollection<string> All =
    [
        Later,
        Refund,
        Offset
    ];
}

public sealed class TknQttNextStepResponse
{
    public Guid TknTaxPeriodId { get; init; }
    public int TaxYear { get; init; }
    public decimal AnnualRevenue { get; init; }
    public decimal IncomeBasedPitPaid { get; init; }
    public string Eligibility { get; init; } = null!;
    public bool RequiresPaymentSourceReview { get; init; }
    public bool CanCreateQttDraft { get; init; }
    public IReadOnlyList<string> Choices { get; init; } = [];
    public IReadOnlyList<QttPreviewIssue> BlockingIssues { get; init; } = [];
    public string? SelectedChoice { get; init; }
    public DateTime? SelectedChoiceAt { get; init; }
    public Guid? QttTaxPeriodId { get; init; }
    public Guid? QttDeclarationId { get; init; }
    public string? QttDeclarationStatus { get; init; }
    public int? QttDraftRevision { get; init; }
}

public sealed class ApplyTknQttNextStepRequest
{
    public string Choice { get; init; } = null!;
    public Guid? RefundPaymentAccountId { get; init; }
    public IReadOnlyList<QttOffsetAllocationItemRequest> OffsetItems { get; init; } = [];
}
