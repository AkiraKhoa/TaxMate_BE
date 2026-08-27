using System.Text.RegularExpressions;

namespace TaxMate.Service.Common;

/// <summary>
/// Produces stable, collision-resistant backend document numbers from the
/// source identity. It intentionally does not depend on row counts.
/// </summary>
public static partial class AccountingDocumentNumber
{
    public static string FromSource(string prefix, Guid sourceId)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("Source id cannot be empty.", nameof(sourceId));
        }

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        if (!ValidPrefix().IsMatch(normalizedPrefix))
        {
            throw new ArgumentException(
                "Prefix must contain 1-12 ASCII letters or digits.",
                nameof(prefix));
        }

        return $"{normalizedPrefix}-{sourceId:N}";
    }

    [GeneratedRegex("^[A-Z0-9]{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPrefix();
}
