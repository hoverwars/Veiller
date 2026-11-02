using Microsoft.Extensions.Options;
using Veiller.Api.Data;

namespace Veiller.Api.Retention;

/// <summary>Runs <see cref="HealthRetentionService"/> at startup, then every <see cref="RetentionOptions.Interval"/>.</summary>
public sealed class HealthRetentionWorker(
    IServiceScopeFactory scopes, IOptions<RetentionOptions> options, TimeProvider clock, ILogger<HealthRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;

        using var timer = new PeriodicTimer(settings.Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<VeillerDbContext>();
                var result = await HealthRetentionService.PurgeAsync(db, clock.GetUtcNow(), settings.HealthDataDays, stoppingToken);
                // Counts only, like the rest of the health logs.
                logger.LogInformation(
                    "Retention purge: {HeartRateRecords} heart rate records, {StepsHourly} steps buckets, {SleepSessions} sleep sessions deleted",
                    result.HeartRateRecords, result.StepsHourly, result.SleepSessions);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Retention purge failed; will retry at the next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
