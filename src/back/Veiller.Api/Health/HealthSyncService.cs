using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Health;

public static class HealthSyncService
{
    /// <summary>
    /// Stores a validated plan in one transaction (all of it or none) and builds the response.
    /// Everything is keyed by its natural key, so replaying the same request leaves the data as after the first time.
    /// </summary>
    public static async Task<SyncResponse> ApplyAsync(
        VeillerDbContext db, TimeProvider clock, string elderId, SyncPlan plan, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        try
        {
            await ApplyOnceAsync(db, now.UtcDateTime, elderId, plan, ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request of the same elder may have inserted the same keys first: replay once against fresh data.
            db.ChangeTracker.Clear();
            await ApplyOnceAsync(db, now.UtcDateTime, elderId, plan, ct);
        }

        return new SyncResponse(
            new SyncAccepted(plan.AcceptedHeartRateSamples, plan.StepsHourly.Count, plan.SleepSessions.Count, plan.AcceptedDeleted),
            new SyncRejected(
                plan.RejectedHeartRateRecords, plan.RejectedStepsHourly, plan.RejectedSleepSessions, plan.RejectedDeleted,
                plan.RejectedWatchLastSeenAt),
            now);
    }

    private static async Task ApplyOnceAsync(VeillerDbContext db, DateTime now, string elderId, SyncPlan plan, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await ReplaceHeartRateAsync(db, now, elderId, plan.HeartRateRecords, ct);
        await UpsertStepsAsync(db, now, elderId, plan.StepsHourly, ct);
        await ReplaceSleepAsync(db, now, elderId, plan.SleepSessions, ct);
        await db.SaveChangesAsync(ct);

        // Deletions come last: an id upserted and deleted in the same request ends up deleted.
        var deletedHeartRate = plan.DeletedHeartRateRecords.ToList();
        if (deletedHeartRate.Count > 0)
            await db.HeartRateRecords.Where(r => r.ElderId == elderId && deletedHeartRate.Contains(r.HcRecordId)).ExecuteDeleteAsync(ct);
        var deletedSleep = plan.DeletedSleepSessions.ToList();
        if (deletedSleep.Count > 0)
            await db.SleepSessions.Where(s => s.ElderId == elderId && deletedSleep.Contains(s.HcRecordId)).ExecuteDeleteAsync(ct);

        await UpdateStatusAsync(db, now, elderId, plan.WatchLastSeenAt, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Replace: the stored samples of a record are deleted (cascade) and the received ones inserted.</summary>
    private static async Task ReplaceHeartRateAsync(
        VeillerDbContext db, DateTime now, string elderId, List<PlannedHeartRateRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return;
        var ids = records.Select(r => r.HcRecordId).ToList();
        await db.HeartRateRecords.Where(r => r.ElderId == elderId && ids.Contains(r.HcRecordId)).ExecuteDeleteAsync(ct);

        foreach (var r in records)
        {
            db.HeartRateRecords.Add(new HeartRateRecord
            {
                ElderId = elderId, HcRecordId = r.HcRecordId, Origin = r.Origin, Start = r.Start, End = r.End, UpdatedAt = now,
                Samples = r.Samples.Select(s => new HeartRateSample
                    { ElderId = elderId, HcRecordId = r.HcRecordId, Time = s.Time, Bpm = s.Bpm }).ToList(),
            });
        }
    }

    /// <summary>Upsert by hour; the last value received wins, zero included.</summary>
    private static async Task UpsertStepsAsync(
        VeillerDbContext db, DateTime now, string elderId, List<PlannedStepsHour> buckets, CancellationToken ct)
    {
        if (buckets.Count == 0) return;
        var starts = buckets.Select(b => b.Start).ToList();
        var existing = await db.StepsHourly
            .Where(s => s.ElderId == elderId && starts.Contains(s.Start))
            .ToDictionaryAsync(s => s.Start, ct);

        foreach (var b in buckets)
        {
            if (existing.TryGetValue(b.Start, out var row))
            {
                row.End = b.End;
                row.Count = b.Count;
                row.UpdatedAt = now;
            }
            else
            {
                db.StepsHourly.Add(new StepsHour { ElderId = elderId, Start = b.Start, End = b.End, Count = b.Count, UpdatedAt = now });
            }
        }
    }

    /// <summary>Upsert replaces the whole session, stages included.</summary>
    private static async Task ReplaceSleepAsync(
        VeillerDbContext db, DateTime now, string elderId, List<PlannedSleepSession> sessions, CancellationToken ct)
    {
        if (sessions.Count == 0) return;
        var ids = sessions.Select(s => s.HcRecordId).ToList();
        await db.SleepSessions.Where(s => s.ElderId == elderId && ids.Contains(s.HcRecordId)).ExecuteDeleteAsync(ct);

        foreach (var s in sessions)
        {
            db.SleepSessions.Add(new SleepSession
            {
                ElderId = elderId, HcRecordId = s.HcRecordId, Origin = s.Origin, Start = s.Start, End = s.End,
                ZoneOffset = s.ZoneOffset, UpdatedAt = now,
                Stages = s.Stages.Select((st, i) => new SleepStage
                    { ElderId = elderId, HcRecordId = s.HcRecordId, Ordinal = i, Start = st.Start, End = st.End, Stage = st.Stage }).ToList(),
            });
        }
    }

    /// <summary>Every successful sync counts as an upload, empty or not. The liveness signal only moves forward.</summary>
    private static async Task UpdateStatusAsync(VeillerDbContext db, DateTime now, string elderId, DateTime? watchLastSeenAt, CancellationToken ct)
    {
        var status = await db.HealthSyncStatuses.SingleOrDefaultAsync(s => s.ElderId == elderId, ct);
        if (status is null)
        {
            status = new HealthSyncStatus { ElderId = elderId };
            db.HealthSyncStatuses.Add(status);
        }

        status.LastUploadAt = now;
        if (watchLastSeenAt is { } seen && (status.WatchLastSeenAt is null || seen > status.WatchLastSeenAt))
            status.WatchLastSeenAt = seen;
    }

    /// <summary>What the server holds for the elder. Every field is null while there is no data.</summary>
    public static async Task<HealthStatusDto> GetStatusAsync(VeillerDbContext db, string elderId, CancellationToken ct)
    {
        var status = await db.HealthSyncStatuses.AsNoTracking().SingleOrDefaultAsync(s => s.ElderId == elderId, ct);
        var heartRate = await db.HeartRateSamples.Where(s => s.ElderId == elderId).MaxAsync(s => (DateTime?)s.Time, ct);
        var steps = await db.StepsHourly.Where(s => s.ElderId == elderId && s.Count > 0).MaxAsync(s => (DateTime?)s.End, ct);
        var sleep = await db.SleepSessions.Where(s => s.ElderId == elderId).MaxAsync(s => (DateTime?)s.End, ct);

        return new HealthStatusDto(
            status?.LastUploadAt.ToUtcOffset(), status?.WatchLastSeenAt.ToUtcOffset(),
            new LastSampleDto(heartRate.ToUtcOffset(), steps.ToUtcOffset(), sleep.ToUtcOffset()));
    }
}

public static class HealthTimeExtensions
{
    /// <summary>Health timestamps are stored as UTC <see cref="DateTime"/>; providers may hand them back with an unspecified kind.</summary>
    public static DateTimeOffset ToUtcOffset(this DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? ToUtcOffset(this DateTime? value) => value?.ToUtcOffset();
}
