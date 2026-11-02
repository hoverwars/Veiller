using Microsoft.EntityFrameworkCore;
using Veiller.Api.Consents;
using Veiller.Api.Data;

namespace Veiller.Api.Patients;

public static class PatientHistoryService
{
    public static async Task<PatientHistoryDto> BuildAsync(
        VeillerDbContext db, TimeProvider clock, string elderId, HistoryRange range, TimeSpan offset, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var buckets = HistoryWindow.Buckets(range, nowUtc, offset);
        var consents = await ConsentService.GetAsync(db, elderId, ct);
        bool Granted(HealthDataType type) => consents.Single(c => c.Type == type).Granted;

        var heart = Granted(HealthDataType.HeartRate) ? await RestingHeartRateAsync(db, elderId, buckets, ct) : [];
        var sleep = Granted(HealthDataType.Sleep) ? await SleepHoursAsync(db, elderId, range, buckets, nowUtc, offset, ct) : [];

        // A patient who never synced has nothing to chart: empty series rather than a row of nulls.
        return new PatientHistoryDto(range, heart.Any(p => p.Value is not null) ? heart : [], sleep.Any(p => p.Value is not null) ? sleep : []);
    }

    private static async Task<IReadOnlyList<DataPointDto>> RestingHeartRateAsync(
        VeillerDbContext db, string elderId, IReadOnlyList<HistoryBucket> buckets, CancellationToken ct)
    {
        var from = buckets[0].StartUtc;
        var to = buckets[^1].EndUtc;
        var samples = (await db.HeartRateSamples.AsNoTracking()
                .Where(s => s.ElderId == elderId && s.Time >= from && s.Time < to)
                .Select(s => new { s.Time, s.Bpm })
                .ToListAsync(ct))
            .Select(s => (s.Time, s.Bpm))
            .ToList();

        return buckets
            .Select(b => new DataPointDto(
                b.Label,
                PatientMetrics.RestingHeartRate(samples.Where(s => s.Time >= b.StartUtc && s.Time < b.EndUtc)),
                Utc(b.StartUtc)))
            .ToList();
    }

    private static async Task<IReadOnlyList<DataPointDto>> SleepHoursAsync(
        VeillerDbContext db, string elderId, HistoryRange range, IReadOnlyList<HistoryBucket> buckets,
        DateTime nowUtc, TimeSpan offset, CancellationToken ct)
    {
        if (range == HistoryRange.Day)
        {
            var minutes = await PatientSummaryService.LastNightMinutesAsync(db, elderId, nowUtc, ct);
            return [new DataPointDto("Anoche", minutes is { } m ? ToHours(m) : null, Utc(buckets[0].StartUtc))];
        }

        // A night is attributed to the local day it ends on; the margin covers sessions recorded in another zone.
        var from = buckets[0].StartUtc.AddDays(-1);
        var to = buckets[^1].EndUtc.AddDays(1);
        var sessions = await db.SleepSessions.AsNoTracking().Include(s => s.Stages)
            .Where(s => s.ElderId == elderId && s.End >= from && s.End < to)
            .ToListAsync(ct);
        var minutesByDay = sessions
            .GroupBy(s => PatientMetrics.LocalDayOf(s, offset))
            .ToDictionary(g => g.Key, g => g.Sum(PatientMetrics.AsleepMinutes));

        return buckets
            .Select(b => new DataPointDto(b.Label, minutesByDay.TryGetValue(DateOnly.FromDateTime((b.StartUtc + offset).Date), out var m) ? ToHours(m) : null, Utc(b.StartUtc)))
            .ToList();
    }

    private static float ToHours(int minutes) => (float)Math.Round(minutes / 60.0, 2);

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
