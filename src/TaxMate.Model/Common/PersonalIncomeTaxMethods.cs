namespace TaxMate.Model.Common;

public static class PersonalIncomeTaxMethods
{
    public const string RevenueBased = "RevenueBased";
    public const string IncomeBased = "IncomeBased";

    public static readonly IReadOnlyCollection<string> All =
    [
        RevenueBased,
        IncomeBased
    ];
}
