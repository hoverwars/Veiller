using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;

namespace Veiller.Api.Retention;

public sealed record RetentionResult(int HeartRateRecords, int StepsHourly, int SleepSessions)
{
    public int Total => HeartRateRecords + StepsHourly + SleepSessions;
}

public static class HealthRetentionService
{
    /// <summary>
    /// Deletes the health data that ended before the retention limit. Samples and sleep stages go with their
    /// parent (cascade). A record is judged on its end, so a long record that is still partly recent is kept whole.
    /// </summary>
    public static async Task<RetentionResult> PurgeAsync(VeillerDbContext db, DateTimeOffset now, int retentionDays, CancellationToken ct)
    {
        var cutoff = now.UtcDateTime.AddDays(-retentionDays);

        var heartRate = await db.HeartRateRecords.Where(r => r.End < cutoff).ExecuteDeleteAsync(ct);
        var steps = await db.StepsHourly.Where(s => s.End < cutoff).ExecuteDeleteAsync(ct);
        var sleep = await db.SleepSessions.Where(s => s.End < cutoff).ExecuteDeleteAsync(ct);
        return new RetentionResult(heartRate, steps, sleep);
    }
}
