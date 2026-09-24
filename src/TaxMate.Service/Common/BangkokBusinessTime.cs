namespace TaxMate.Service.Common;

/// <summary>
/// Converts Bangkok business wall-clock values to the persistence contract:
/// a UTC instant encoded as DateTimeKind.Unspecified for PostgreSQL
/// timestamp-without-time-zone columns. All windows use [start, end).
/// </summary>
public static class BangkokBusinessTime
{
    private static readonly Lazy<TimeZoneInfo> BangkokTimeZone = new(FindTimeZone);

    public static TimeZoneInfo TimeZone => BangkokTimeZone.Value;

    public static (DateTime StartNaiveUtc, DateTime EndExclusiveNaiveUtc)
        GetCalendarYearNaiveUtc(int year)
    {
        var wallClockStart = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified);

        return (
            BangkokWallClockToNaiveUtc(wallClockStart),
            BangkokWallClockToNaiveUtc(wallClockStart.AddYears(1)));
    }

    public static (DateTime StartNaiveUtc, DateTime EndExclusiveNaiveUtc)
        GetQuarterNaiveUtc(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quarter),
                "Quarter must be between 1 and 4.");
        }

        var wallClockStart = new DateTime(
            year,
            ((quarter - 1) * 3) + 1,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified);

        return (
            BangkokWallClockToNaiveUtc(wallClockStart),
            BangkokWallClockToNaiveUtc(wallClockStart.AddMonths(3)));
    }

    public static DateTime BangkokWallClockToNaiveUtc(DateTime bangkokWallClock)
    {
        RequireKind(
            bangkokWallClock,
            DateTimeKind.Unspecified,
            "Bangkok wall-clock values must use DateTimeKind.Unspecified.");

        var utc = TimeZoneInfo.ConvertTimeToUtc(bangkokWallClock, TimeZone);
        return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
    }

    public static DateTime NaiveUtcToBangkokWallClock(DateTime naiveUtc)
    {
        RequireNaiveUtc(naiveUtc, nameof(naiveUtc));

        var utc = DateTime.SpecifyKind(naiveUtc, DateTimeKind.Utc);
        var wallClock = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
        return DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// UTC is stripped to the database's naive-UTC encoding; Unspecified is
    /// already that encoding. Local is rejected because its meaning depends on
    /// the host machine timezone.
    /// </summary>
    public static DateTime NormalizeNaiveUtc(DateTime instant)
    {
        return instant.Kind switch
        {
            DateTimeKind.Unspecified => instant,
            DateTimeKind.Utc => DateTime.SpecifyKind(
                instant,
                DateTimeKind.Unspecified),
            _ => throw new ArgumentException(
                "DateTimeKind.Local is not accepted for accounting instants.",
                nameof(instant))
        };
    }

    public static void RequireNaiveUtc(DateTime instant, string parameterName)
    {
        if (instant.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "Expected a UTC instant encoded as DateTimeKind.Unspecified.",
                parameterName);
        }
    }

    public static int GetBangkokCalendarYear(DateTime naiveUtc)
    {
        return NaiveUtcToBangkokWallClock(naiveUtc).Year;
    }

    public static bool ContainsNaiveUtc(
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        DateTime occurrenceNaiveUtc)
    {
        RequireNaiveUtc(startNaiveUtc, nameof(startNaiveUtc));
        RequireNaiveUtc(endExclusiveNaiveUtc, nameof(endExclusiveNaiveUtc));
        RequireNaiveUtc(occurrenceNaiveUtc, nameof(occurrenceNaiveUtc));

        return occurrenceNaiveUtc >= startNaiveUtc &&
               occurrenceNaiveUtc < endExclusiveNaiveUtc;
    }

    private static void RequireKind(
        DateTime value,
        DateTimeKind expected,
        string message)
    {
        if (value.Kind != expected)
        {
            throw new ArgumentException(message, nameof(value));
        }
    }

    private static TimeZoneInfo FindTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Bangkok" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the platform-specific alternative.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the platform-specific alternative.
            }
        }

        throw new TimeZoneNotFoundException(
            "Neither 'SE Asia Standard Time' nor 'Asia/Bangkok' is available.");
    }
}
