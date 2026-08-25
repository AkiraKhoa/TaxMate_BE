namespace TaxMate.Model.Common;

public static class CommencementPeriods
{
    public const string BeforeTaxYear = "BeforeTaxYear";
    public const string FirstHalfOfTaxYear = "FirstHalfOfTaxYear";
    public const string SecondHalfOfTaxYear = "SecondHalfOfTaxYear";

    public static readonly IReadOnlyCollection<string> All =
    [
        BeforeTaxYear,
        FirstHalfOfTaxYear,
        SecondHalfOfTaxYear
    ];
}
