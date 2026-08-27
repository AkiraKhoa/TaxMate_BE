namespace TaxMate.Model.Common;

public static class PersonalIncomeTaxMethods
{
    public const string RevenueBased = "RevenueBased";
    public const string IncomeBased = "IncomeBased";
    public const string NotApplicable = "NotApplicable";

    public static readonly IReadOnlyCollection<string> All =
    [
        RevenueBased,
        IncomeBased,
        NotApplicable
    ];
}
