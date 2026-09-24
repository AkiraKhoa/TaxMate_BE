namespace TaxMate.Model.DTO.TaxProfile;

public sealed class OwnerTaxProfileResponse
{
    public Guid BusinessId { get; init; }
    public string? DeclaredRevenueBracket { get; init; }
    public string? PersonalIncomeTaxMethod { get; init; }
    public int? TaxMethodEffectiveYear { get; init; }
    public string? CommencementPeriod { get; init; }
    public int? CommencementTaxYear { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public bool IsConfigured { get; init; }
    public bool IsMethodLocked { get; init; }
    public int? LockedThroughYear { get; init; }
    public IReadOnlyList<RevenueThresholdReviewResponse> ThresholdReviews { get; init; } = [];
}

public sealed class RevenueThresholdReviewResponse
{
    public Guid AlertId { get; init; }
    public int Year { get; init; }
    public int Quarter { get; init; }
    public string ThresholdCode { get; init; } = null!;
    public decimal ThresholdAmount { get; init; }
    public decimal CurrentAnnualRevenue { get; init; }
    public string Status { get; init; } = null!;
    public bool CanConfirm { get; init; }
    public bool CanDismiss { get; init; }
    public string? RequiredTaxMethod { get; init; }
    public IReadOnlyList<string> AllowedTaxMethods { get; init; } = [];
    public int AppliesFromYear { get; init; }
    public bool IsOutsideSupportedScope { get; init; }
    public string Message { get; init; } = null!;
}

public sealed class UpdateOwnerTaxProfileRequest
{
    public string DeclaredRevenueBracket { get; init; } = null!;
    public string? PersonalIncomeTaxMethod { get; init; }
    public string? CommencementPeriod { get; init; }
    public int? CommencementTaxYear { get; init; }
    public bool Confirmed { get; init; }
}

public sealed class ConfirmRevenueThresholdReviewRequest
{
    public string? PersonalIncomeTaxMethod { get; init; }
    public bool Confirmed { get; init; }
}
