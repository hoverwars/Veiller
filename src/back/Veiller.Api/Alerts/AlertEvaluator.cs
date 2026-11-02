using Microsoft.EntityFrameworkCore;
using Veiller.Api.Consents;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;
using Veiller.Api.Patients;

namespace Veiller.Api.Alerts;

/// <summary>
/// Compares the elder's latest data with the circle's rules. One alert per episode: a condition that stays true keeps
/// its alert, and an alert is closed only when the condition is seen to end. Without evidence (no recent reading)
/// nothing changes, so a quiet watch never "resolves" a heart rate alert by itself.
/// </summary>
public static class AlertEvaluator
{
    /// <summary>Evaluates one circle and returns the alerts opened by this run (to notify).</summary>
    public static async Task<List<Alert>> EvaluateAsync(
        VeillerDbContext db, TimeProvider clock, AlertOptions options, Guid circleId, string ownerId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rules = AlertRuleSet.From(options, await db.AlertRules.AsNoTracking().Where(r => r.CircleId == circleId).ToListAsync(ct));
        var consents = await ConsentService.GetAsync(db, ownerId, ct);
        bool Granted(HealthDataType type) => consents.Single(c => c.Type == type).Granted;

        var verdicts = new List<(AlertType Type, bool? Breached, int? Value)>();

        if (Granted(HealthDataType.HeartRate))
        {
            var since = now.UtcDateTime - options.HeartRateFreshness;
            var bpm = await db.HeartRateSamples.AsNoTracking()
                .Where(s => s.ElderId == ownerId && s.Time > since && s.Time <= now.UtcDateTime)
                .OrderByDescending(s => s.Time).Select(s => (int?)s.Bpm).FirstOrDefaultAsync(ct);
            verdicts.Add((AlertType.HeartRateHigh, bpm is { } hi ? hi > rules.Threshold(AlertType.HeartRateHigh) : null, bpm));
            verdicts.Add((AlertType.HeartRateLow, bpm is { } lo ? lo < rules.Threshold(AlertType.HeartRateLow) : null, bpm));
        }

        if (Granted(HealthDataType.Sleep))
        {
            var minutes = await PatientSummaryService.LastNightMinutesAsync(db, ownerId, now.UtcDateTime, ct);
            verdicts.Add((AlertType.SleepLow, minutes is { } m ? m < rules.Threshold(AlertType.SleepLow) : null, minutes));
        }

        var status = await db.HealthSyncStatuses.AsNoTracking().SingleOrDefaultAsync(s => s.ElderId == ownerId, ct);
        if (status is not null)
        {
            var lastSeen = new DateTimeOffset(DateTime.SpecifyKind(status.WatchLastSeenAt ?? status.LastUploadAt, DateTimeKind.Utc));
            var silentMinutes = (int)Math.Max(0, (now - lastSeen).TotalMinutes);
            verdicts.Add((AlertType.NoSync, silentMinutes > rules.Threshold(AlertType.NoSync), silentMinutes));
        }

        var active = await db.Alerts.Where(a => a.CircleId == circleId && a.Active).ToListAsync(ct);
        var opened = new List<Alert>();
        foreach (var (type, breached, value) in verdicts)
        {
            var current = active.FirstOrDefault(a => a.Type == type);
            if (breached is null) continue;

            if (!rules.IsEnabled(type))
            {
                // A disabled rule neither opens nor keeps an alert.
                if (current is not null) Close(current, now);
            }
            else if (breached.Value && current is null)
            {
                var alert = new Alert { CircleId = circleId, Type = type, Status = AlertStatus.New, OpenedAt = now, Value = value };
                db.Alerts.Add(alert);
                opened.Add(alert);
            }
            else if (!breached.Value && current is not null)
            {
                Close(current, now);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent evaluation (sync and worker) opened the same episode first: its alert is the one that counts.
            db.ChangeTracker.Clear();
            return [];
        }
        return opened;
    }

    /// <summary>Ends an episode; an alert nobody resolved is resolved automatically.</summary>
    private static void Close(Alert alert, DateTimeOffset now)
    {
        alert.Active = false;
        if (alert.Status != AlertStatus.Resolved)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = now;
        }
    }
}
