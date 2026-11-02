using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data;
using Veiller.Api.Data.Configurations;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

/// <summary>Registration of the push token of a device, so a caregiver's phone receives the alerts of their circles.</summary>
public static class DeviceEndpoints
{
    private static readonly HashSet<string> Platforms = ["android", "ios", "web"];

    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices").RequireAuthorization();

        group.MapPut("/token", RegisterAsync)
            .WithDescription("Registers (or re-assigns to the caller) a push token. Idempotent. Platform: android, ios or web.");
        group.MapDelete("/token", UnregisterAsync)
            .WithDescription("Removes one of the caller's push tokens (sign-out). Idempotent.");
        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterDeviceRequest? body, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();

        var errors = new Dictionary<string, string[]>();
        var token = body?.Token?.Trim();
        if (string.IsNullOrEmpty(token)) errors["token"] = ["Required."];
        else if (token.Length > DeviceTokenLimits.MaxTokenLength) errors["token"] = [$"At most {DeviceTokenLimits.MaxTokenLength} characters."];
        var platform = body?.Platform?.Trim().ToLowerInvariant();
        if (platform is null || !Platforms.Contains(platform)) errors["platform"] = ["Must be one of: android, ios, web."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var existing = await db.DeviceTokens.SingleOrDefaultAsync(t => t.Token == token, ct);
        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken { Token = token!, UserId = uid, Platform = platform!, UpdatedAt = clock.GetUtcNow() });
        }
        else
        {
            // A phone that changes account keeps its token: it now belongs to the new user.
            existing.UserId = uid;
            existing.Platform = platform!;
            existing.UpdatedAt = clock.GetUtcNow();
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent registrations of the same token: the other one won, which is the result we wanted.
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UnregisterAsync(
        [FromBody] UnregisterDeviceRequest? body, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();

        var token = body?.Token?.Trim();
        if (string.IsNullOrEmpty(token))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["token"] = ["Required."] });

        await db.DeviceTokens.Where(t => t.Token == token && t.UserId == uid).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }
}
