namespace TaxMate.Model.Common;

public static class TaxThresholdTypes
{
    public const string AnnualRevenueTax = "AnnualRevenueTax";
    public const string EInvoiceRequirement = "EInvoiceRequirement";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        AnnualRevenueTax,
        EInvoiceRequirement
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

        return type;
    }
}
