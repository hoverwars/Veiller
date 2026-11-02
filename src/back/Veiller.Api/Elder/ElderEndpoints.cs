using System.Security.Claims;
using Microsoft.Extensions.Options;
using Veiller.Api.Auth;
using Veiller.Api.Data;
using Veiller.Api.Health;

namespace Veiller.Api.Elder;

public static class ElderEndpoints
{
    private const string ElderOnly = "Only the elder can read their own home.";

    public static IEndpointRouteBuilder MapElderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/elder/home", GetHomeAsync)
            .WithTags("Elder").RequireAuthorization()
            .WithDescription("Home of the signed-in elder: minutes since the last upload (null if never), watch status and caregivers of their own circle.");
        return app;
    }

    private static async Task<IResult> GetHomeAsync(
        ClaimsPrincipal principal, VeillerDbContext db, IOptions<HealthSyncOptions> options, TimeProvider clock, CancellationToken ct)
    {
        if (await ElderAccess.RequireElderAsync(principal, db, ct) is not { } elderId) return ElderAccess.Denied(principal, ElderOnly);

        return Results.Ok(await ElderHomeService.BuildAsync(db, clock, options.Value, elderId, ct));
    }
}
