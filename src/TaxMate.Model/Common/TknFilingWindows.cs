namespace TaxMate.Model.Common;

public static class TknFilingWindows
{
    public const string FirstHalf = "FirstHalf";
    public const string SecondHalf = "SecondHalf";
    public const string Annual = "Annual";

    public static readonly IReadOnlyCollection<string> All =
    [
        FirstHalf,
        SecondHalf,
        Annual
    ];
}

public sealed record TknPeriodWindowValue(
    DateTime StartNaiveUtc,
    DateTime EndExclusiveNaiveUtc,
    DateTime DueDateNaiveUtc);

/// <summary>
/// Builds immutable 01/TKN-CNKD filing windows. Values follow TaxMate's
/// persistence convention: UTC instants encoded as DateTimeKind.Unspecified.
/// Bangkok has a fixed UTC+07 offset and does not observe daylight saving time.
/// </summary>
public static class TknPeriodWindow
{
    public static TknPeriodWindowValue Get(int year, string filingWindow)
    {
        if (year is < 2 or > 9998)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "Year must be between 2 and 9998.");
        }

        return filingWindow switch
        {
            TknFilingWindows.FirstHalf => new(
                ToNaiveUtc(year, 1, 1),
                ToNaiveUtc(year, 7, 1),
                ToNaiveUtc(year, 7, 31)),
            TknFilingWindows.SecondHalf => new(
                ToNaiveUtc(year, 7, 1),
                ToNaiveUtc(year + 1, 1, 1),
                ToNaiveUtc(year + 1, 1, 31)),
            TknFilingWindows.Annual => new(
                ToNaiveUtc(year, 1, 1),
                ToNaiveUtc(year + 1, 1, 1),
                ToNaiveUtc(year + 1, 1, 31)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(filingWindow),
                filingWindow,
                "Unsupported TKN filing window.")
        };
    }

    private static DateTime ToNaiveUtc(int year, int month, int day)
    {
        var bangkokMidnight = new DateTime(
            year,
            month,
            day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);

        return DateTime.SpecifyKind(
            bangkokMidnight.AddHours(-7),
            DateTimeKind.Unspecified);
    }
}
