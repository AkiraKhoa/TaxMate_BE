namespace TaxMate.Model.DTO.TaxFiling;

public sealed class TaxFilingTaskSummaryResponse
{
    public string TaskId { get; init; } = null!;
    public string FilingType { get; init; } = null!;
    public string FormCode { get; init; } = null!;
    public int TaxYear { get; init; }
    public TaxFilingWindowResponse Window { get; init; } = null!;
    public DateOnly? Deadline { get; init; }
    public string Status { get; init; } = null!;
    public bool IsOverdue { get; init; }
    public TaxFilingTaskReasonResponse Reason { get; init; } = null!;
    public TaxFilingEligibilityResponse Eligibility { get; init; } = null!;
    public TaxFilingTaskActionResponse PrimaryAction { get; init; } = null!;
    public Guid? TaxPeriodId { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class TaxFilingWindowResponse
{
    public string Code { get; init; } = null!;
    public DateOnly FromInclusive { get; init; }
    public DateOnly ToExclusive { get; init; }
    public string Label { get; init; } = null!;
}

public sealed class TaxFilingTaskReasonResponse
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
}

public sealed class TaxFilingEligibilityResponse
{
    public bool IsEligible { get; init; }
    public IReadOnlyList<TaxFilingTaskBlockerResponse> Blockers { get; init; } = [];
}

public sealed class TaxFilingTaskBlockerResponse
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
}

public sealed class TaxFilingTaskActionResponse
{
    public string Code { get; init; } = null!;
    public bool Enabled { get; init; }
}

public static class TaxFilingTaskStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Ready = "Ready";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Blocked = "Blocked";
    public const string NotApplicable = "NotApplicable";
}

public static class TaxFilingTaskActions
{
    public const string Open = "Open";
    public const string Continue = "Continue";
    public const string View = "View";
    public const string None = "None";
}

public static class TaxFilingTaskReasons
{
    public const string NewBusinessFirstHalf = "NewBusinessFirstHalf";
    public const string NewBusinessSecondHalf = "NewBusinessSecondHalf";
    public const string AnnualAtOrBelowThreshold = "AnnualAtOrBelowThreshold";
}

public static class TaxFilingTaskBlockerCodes
{
    public const string TaxProfileUnconfirmed = "TaxProfileUnconfirmed";
    public const string TaxProfileIncompatible = "TaxProfileIncompatible";
    public const string CommencementDataMissing = "CommencementDataMissing";
    public const string NotAtOrBelowThreshold = "NotAtOrBelowThreshold";
    public const string SourceDataInvalid = "SourceDataInvalid";
    public const string WindowNotStarted = "WindowNotStarted";
}
