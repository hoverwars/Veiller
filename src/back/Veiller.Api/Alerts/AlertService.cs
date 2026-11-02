using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

public enum AlertChange
{
    Done,
    NotFound,
}

public static class AlertService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static async Task<List<AlertDto>> ListAsync(
        VeillerDbContext db, Guid circleId, AlertStatus? status, int limit, CancellationToken ct)
    {
        var query = db.Alerts.AsNoTracking().Where(a => a.CircleId == circleId);
        if (status is { } s) query = query.Where(a => a.Status == s);

        var rows = await query.ToListAsync(ct);
        // Ordered in memory: DateTimeOffset has no ORDER BY on SQLite, and one circle has few alerts.
        return rows.OrderByDescending(a => a.OpenedAt).Take(limit).Select(ToDto).ToList();
    }

    /// <summary>Marks an alert as seen. An alert already seen or resolved is left as it is (idempotent).</summary>
    public static async Task<AlertChange> MarkSeenAsync(VeillerDbContext db, Guid circleId, Guid alertId, CancellationToken ct)
    {
        var alert = await db.Alerts.SingleOrDefaultAsync(a => a.CircleId == circleId && a.Id == alertId, ct);
        if (alert is null) return AlertChange.NotFound;

        if (alert.Status == AlertStatus.New)
        {
            alert.Status = AlertStatus.Seen;
            await db.SaveChangesAsync(ct);
        }
        return AlertChange.Done;
    }

    /// <summary>
    /// Resolves an alert by hand. The episode stays <see cref="Alert.Active"/> until the evaluator sees the condition end,
    /// so resolving does not make the same alert come back at the next evaluation.
    /// </summary>
    public static async Task<AlertChange> ResolveAsync(
        VeillerDbContext db, TimeProvider clock, Guid circleId, Guid alertId, CancellationToken ct)
    {
        var alert = await db.Alerts.SingleOrDefaultAsync(a => a.CircleId == circleId && a.Id == alertId, ct);
        if (alert is null) return AlertChange.NotFound;

        if (alert.Status != AlertStatus.Resolved)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        return AlertChange.Done;
    }

    public static async Task<AlertRuleSet> GetRulesAsync(VeillerDbContext db, AlertOptions options, Guid circleId, CancellationToken ct) =>
        AlertRuleSet.From(options, await db.AlertRules.AsNoTracking().Where(r => r.CircleId == circleId).ToListAsync(ct));

    /// <summary>Stores the rules of the request as overrides (the types it does not mention keep their current rule).</summary>
    public static async Task SaveRulesAsync(
        VeillerDbContext db, Guid circleId, IReadOnlyList<(AlertType Type, bool Enabled, int Threshold)> changes, CancellationToken ct)
    {
        var existing = await db.AlertRules.Where(r => r.CircleId == circleId).ToDictionaryAsync(r => r.Type, ct);
        foreach (var (type, enabled, threshold) in changes)
        {
            if (existing.TryGetValue(type, out var row))
            {
                row.Enabled = enabled;
                row.Threshold = threshold;
            }
            else
            {
                db.AlertRules.Add(new AlertRule { CircleId = circleId, Type = type, Enabled = enabled, Threshold = threshold });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static AlertDto ToDto(Alert a) => new(a.Id, a.CircleId, a.Type, a.Status, a.OpenedAt, a.ResolvedAt, a.Value);
}
