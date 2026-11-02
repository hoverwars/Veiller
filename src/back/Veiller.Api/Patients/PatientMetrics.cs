using Veiller.Api.Data.Entities;
using Veiller.Api.Health;

namespace Veiller.Api.Patients;

/// <summary>Pure aggregation rules shared by the summary and the history (no database access, easy to test).</summary>
public static class PatientMetrics
{
    private static readonly HashSet<string> NotAsleep = ["AWAKE", "AWAKE_IN_BED", "OUT_OF_BED"];

    /// <summary>Minutes actually asleep: the stages that are not awake/out of bed; a session without stages counts entirely.</summary>
    public static int AsleepMinutes(SleepSession session)
    {
        var minutes = session.Stages.Count == 0
            ? (session.End - session.Start).TotalMinutes
            : session.Stages.Where(s => !NotAsleep.Contains(s.Stage)).Sum(s => (s.End - s.Start).TotalMinutes);
        return (int)Math.Round(Math.Max(0, minutes));
    }

    /// <summary>The offset a session was recorded in, or <paramref name="fallback"/> when the writer gave none.</summary>
    public static TimeSpan OffsetOf(SleepSession session, TimeSpan fallback) =>
        UtcOffsets.TryParse(session.ZoneOffset, out var offset) ? offset : fallback;

    /// <summary>The local calendar date a night is attributed to: the date it ends on, in the zone it was recorded in.</summary>
    public static DateOnly LocalDayOf(SleepSession session, TimeSpan fallback) =>
        DateOnly.FromDateTime((session.End + OffsetOf(session, fallback)).Date);

    /// <summary>
    /// Resting heart rate of a set of samples: apps may write the same minute twice, so samples are first averaged per
    /// minute, then the lowest quarter of the minutes is averaged. Null without samples.
    /// </summary>
    public static float? RestingHeartRate(IEnumerable<(DateTime Time, int Bpm)> samples)
    {
        var perMinute = samples
            .GroupBy(s => s.Time.Ticks / TimeSpan.TicksPerMinute)
            .Select(g => g.Average(s => s.Bpm))
            .OrderBy(v => v)
            .ToList();
        if (perMinute.Count == 0) return null;
        var lowest = perMinute.Take((perMinute.Count + 3) / 4);
        return (float)Math.Round(lowest.Average(), 1);
    }
}
