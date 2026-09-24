namespace TaxMate.Model.Common;

public static class TaxThresholdTypes
{
    public const string AnnualRevenueTax = "AnnualRevenueTax";
    public const string EInvoiceRequirement = "EInvoiceRequirement";
    public const string IncomeBasedRequirement = "IncomeBasedRequirement";
    public const string SupportedRevenueCeiling = "SupportedRevenueCeiling";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        AnnualRevenueTax,
        EInvoiceRequirement,
        IncomeBasedRequirement,
        SupportedRevenueCeiling
    };

    public static string Normalize(string type)
    {
        if (type.Equals(AnnualRevenueTax, StringComparison.OrdinalIgnoreCase))
        {
            return AnnualRevenueTax;
        }

        if (type.Equals(EInvoiceRequirement, StringComparison.OrdinalIgnoreCase))
        {
            return EInvoiceRequirement;
        }

        if (type.Equals(IncomeBasedRequirement, StringComparison.OrdinalIgnoreCase))
        {
            return IncomeBasedRequirement;
        }

        if (type.Equals(SupportedRevenueCeiling, StringComparison.OrdinalIgnoreCase))
        {
            return SupportedRevenueCeiling;
        }

        return type;
    }
}
