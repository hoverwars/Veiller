namespace Veiller.Api.Patients;

/// <summary>One slice of an history chart: <c>[StartUtc, EndUtc)</c> plus its display label.</summary>
public sealed record HistoryBucket(DateTime StartUtc, DateTime EndUtc, string Label);

/// <summary>Pure calendar maths of the history charts: which buckets a range has, in the caller's local time.</summary>
public static class HistoryWindow
{
    // Spanish weekday initials, indexed by DayOfWeek (Sunday = 0), as in the mobile mock (J V S D L M X).
    private static readonly string[] WeekdayInitials = ["D", "L", "M", "X", "J", "V", "S"];

    /// <summary>Midnight of the local day containing <paramref name="nowUtc"/>, expressed in UTC.</summary>
    public static DateTime LocalDayStartUtc(DateTime nowUtc, TimeSpan offset) => (nowUtc + offset).Date - offset;

    public static IReadOnlyList<HistoryBucket> Buckets(HistoryRange range, DateTime nowUtc, TimeSpan offset)
    {
        var today = LocalDayStartUtc(nowUtc, offset);
        return range switch
        {
            HistoryRange.Day => Enumerable.Range(0, 8)
                .Select(i => new HistoryBucket(today.AddHours(i * 3), today.AddHours(i * 3 + 3), $"{i * 3}h")).ToList(),
            HistoryRange.Week => Days(today, 7, (day, _) => WeekdayInitials[(int)(day + offset).DayOfWeek]),
            _ => Days(today, 30, (day, index) => index % 5 == 0 ? (day + offset).Day.ToString() : string.Empty),
        };
    }

    private static List<HistoryBucket> Days(DateTime today, int count, Func<DateTime, int, string> label) =>
        Enumerable.Range(0, count)
            .Select(i => today.AddDays(i - (count - 1)))
            .Select((day, i) => new HistoryBucket(day, day.AddDays(1), label(day, i)))
            .ToList();
}
