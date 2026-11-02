using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Veiller.Api.Data;

namespace Veiller.Api.Alerts;

/// <summary>
/// Re-evaluates every circle whose elder has synced at least once. Needed for the silence of the watch,
/// which cannot be detected at ingestion because nothing arrives.
/// </summary>
public sealed class AlertWorker(
    IServiceScopeFactory scopes, IOptions<AlertOptions> options, ILogger<AlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.WorkerEnabled) return;

        using var timer = new PeriodicTimer(settings.EvaluationInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await RunOnceAsync(scope.ServiceProvider, settings, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Alert evaluation failed; will retry at the next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass over all circles; public so tests can drive it without the timer. Returns the number of alerts opened.</summary>
    public static async Task<int> RunOnceAsync(IServiceProvider services, AlertOptions settings, CancellationToken ct)
    {
        var db = services.GetRequiredService<VeillerDbContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var notifier = services.GetRequiredService<AlertNotifier>();

        var circles = await db.Circles.AsNoTracking()
            .Where(c => db.HealthSyncStatuses.Any(s => s.ElderId == c.OwnerUserId))
            .Select(c => new { c.Id, c.OwnerUserId })
            .ToListAsync(ct);

        var logger = services.GetRequiredService<ILogger<AlertWorker>>();
        var opened = 0;
        foreach (var circle in circles)
        {
            try
            {
                var alerts = await AlertEvaluator.EvaluateAsync(db, clock, settings, circle.Id, circle.OwnerUserId, ct);
                await notifier.NotifyAsync(alerts, ct);
                opened += alerts.Count;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One failing circle must not stop the others; the context is cleared so the failed changes are not retried.
                logger.LogError(e, "Alert evaluation failed for circle {CircleId}", circle.Id);
                db.ChangeTracker.Clear();
            }
        }
        return opened;
    }
}
