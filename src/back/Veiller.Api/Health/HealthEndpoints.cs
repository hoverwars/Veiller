using System.Security.Claims;
using Microsoft.Extensions.Options;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Health;

public static class HealthEndpoints
{
    private const string ElderOnly = "Only the elder can send or read their own health data.";

    public static IEndpointRouteBuilder MapHealthSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/health/sync", SyncAsync)
            .WithTags("Health").RequireAuthorization()
            .WithDescription("Uploads new, changed and deleted Health Connect records of the signed-in elder. Idempotent; invalid items are skipped and listed in `rejected`.");
        app.MapGet("/api/health/me/status", GetStatusAsync)
            .WithTags("Health").RequireAuthorization()
            .WithDescription("What the server has stored for the signed-in elder: last upload, watch liveness, latest sample per type.");

        return app;
    }

    private static async Task<IResult> SyncAsync(
        HttpRequest http, ClaimsPrincipal principal, VeillerDbContext db, IOptions<HealthSyncOptions> options,
        TimeProvider clock, ILoggerFactory loggers, CancellationToken ct)
    {
        if (await ElderAccess.RequireElderAsync(principal, db, ct) is not { } elderId) return ElderAccess.Denied(principal, ElderOnly);

        var limits = options.Value;
        var (request, error) = await HealthSyncBodyReader.ReadAsync(http, limits.MaxBodyBytes, ct);
        if (error is not null) return error;
        if (request!.SchemaVersion != 1)
            return Results.Problem("Unsupported schemaVersion.", statusCode: StatusCodes.Status400BadRequest);
        if (HealthSyncValidator.CountItems(request) > limits.MaxItemsPerRequest) return HealthSyncBodyReader.TooLarge();

        var plan = HealthSyncValidator.Validate(request, clock.GetUtcNow(), limits.FutureTolerance);
        var response = await HealthSyncService.ApplyAsync(db, clock, elderId, plan, ct);

        // Counts only: health values (and the elder's identity) never reach the logs.
        loggers.CreateLogger("Veiller.Api.Health").LogInformation(
            "Health sync stored: {HeartRateSamples} heart rate samples, {StepsHourly} steps buckets, {SleepSessions} sleep sessions, {Deleted} deletions; {Rejected} items rejected",
            response.Accepted.HeartRateSamples, response.Accepted.StepsHourly, response.Accepted.SleepSessions,
            response.Accepted.Deleted, plan.RejectedCount);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetStatusAsync(ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (await ElderAccess.RequireElderAsync(principal, db, ct) is not { } elderId) return ElderAccess.Denied(principal, ElderOnly);
        return Results.Ok(await HealthSyncService.GetStatusAsync(db, elderId, ct));
    }
}
