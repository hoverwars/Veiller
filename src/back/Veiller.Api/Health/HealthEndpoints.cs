using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Alerts;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Health;

public static class HealthEndpoints
{
    public const string RateLimitPolicy = "health-sync";

    private const string ElderOnly = "Only the elder can send or read their own health data.";

    public static IEndpointRouteBuilder MapHealthSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/health/sync", SyncAsync)
            .WithTags("Health").RequireAuthorization().RequireRateLimiting(RateLimitPolicy)
            .WithDescription("Uploads new, changed and deleted Health Connect records of the signed-in elder. Idempotent; invalid items are skipped and listed in `rejected`.");
        app.MapGet("/api/health/me/status", GetStatusAsync)
            .WithTags("Health").RequireAuthorization()
            .WithDescription("What the server has stored for the signed-in elder: last upload, watch liveness, latest sample per type.");

        return app;
    }

    private static async Task<IResult> SyncAsync(
        HttpRequest http, ClaimsPrincipal principal, VeillerDbContext db, IOptions<HealthSyncOptions> options,
        IOptions<AlertOptions> alertOptions, AlertNotifier notifier, TimeProvider clock, ILoggerFactory loggers, CancellationToken ct)
    {
        if (await ElderAccess.RequireElderAsync(principal, db, ct) is not { } elderId) return ElderAccess.Denied(principal, ElderOnly);

        var limits = options.Value;
        var (request, error) = await HealthSyncBodyReader.ReadAsync(http, limits.MaxBodyBytes, ct);
        if (error is not null) return error;
        if (request!.SchemaVersion != 1)
            return Results.Problem("Unsupported schemaVersion.", statusCode: StatusCodes.Status400BadRequest);
        if (HealthSyncValidator.CountItems(request, limits.MaxSamplesPerRecord) > limits.MaxItemsPerRequest) return HealthSyncBodyReader.TooLarge();

        var plan = HealthSyncValidator.Validate(request, clock.GetUtcNow(), limits);
        var response = await HealthSyncService.ApplyAsync(db, clock, elderId, plan, ct);

        // Counts only: health values (and the elder's identity) never reach the logs.
        loggers.CreateLogger("Veiller.Api.Health").LogInformation(
            "Health sync stored: {HeartRateSamples} heart rate samples, {StepsHourly} steps buckets, {SleepSessions} sleep sessions, {Deleted} deletions; {Rejected} items rejected",
            response.Accepted.HeartRateSamples, response.Accepted.StepsHourly, response.Accepted.SleepSessions,
            response.Accepted.Deleted, plan.RejectedCount);

        await EvaluateAlertsAsync(db, clock, alertOptions.Value, notifier, elderId, loggers.CreateLogger("Veiller.Api.Alerts"), ct);
        return Results.Ok(response);
    }

    /// <summary>
    /// Evaluates the alert rules of the elder's circle with the data just stored. The data is already committed and the client
    /// advances its token on 200, so a failure here is logged and never fails the sync.
    /// </summary>
    private static async Task EvaluateAlertsAsync(
        VeillerDbContext db, TimeProvider clock, AlertOptions options, AlertNotifier notifier, string elderId, ILogger logger, CancellationToken ct)
    {
        try
        {
            if (await db.Circles.AsNoTracking().Where(c => c.OwnerUserId == elderId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct) is not { } circleId)
                return;
            var opened = await AlertEvaluator.EvaluateAsync(db, clock, options, circleId, elderId, ct);
            await notifier.NotifyAsync(opened, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Alert evaluation after a sync failed");
        }
    }

    private static async Task<IResult> GetStatusAsync(ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (await ElderAccess.RequireElderAsync(principal, db, ct) is not { } elderId) return ElderAccess.Denied(principal, ElderOnly);
        return Results.Ok(await HealthSyncService.GetStatusAsync(db, elderId, ct));
    }
}
