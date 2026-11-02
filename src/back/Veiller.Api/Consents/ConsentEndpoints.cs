using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Circles;
using Veiller.Api.Data;

namespace Veiller.Api.Consents;

public static class ConsentEndpoints
{
    public static IEndpointRouteBuilder MapConsentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/consents", GetOwnAsync)
            .WithTags("Consents").RequireAuthorization()
            .WithDescription("Health data consents of the current elder. Defaults apply until they are first saved.");
        app.MapPut("/api/consents", ReplaceOwnAsync)
            .WithTags("Consents").RequireAuthorization()
            .WithDescription("Replaces the consents of the current elder. The body must list every type exactly once; required types cannot be refused.");
        app.MapGet("/api/circles/{id:guid}/consents", GetForCircleAsync)
            .WithTags("Consents").RequireAuthorization()
            .WithDescription("Consents of the circle's owner, readable by any member. 404 for non-members.");

        return app;
    }

    private static async Task<IResult> GetOwnAsync(
        ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (await RequireElderAsync(principal, db, ct) is not { } uid) return Denied(principal);
        return Results.Ok(await ConsentService.GetAsync(db, uid, ct));
    }

    private static async Task<IResult> ReplaceOwnAsync(
        List<ConsentItemRequest?>? items, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await RequireElderAsync(principal, db, ct) is not { } uid) return Denied(principal);

        var errors = ConsentValidator.Validate(items);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        return Results.Ok(await ConsentService.ReplaceAsync(db, clock, uid, items!, ct));
    }

    private static async Task<IResult> GetForCircleAsync(
        Guid id, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        if (await db.FindMembershipAsync(id, uid, ct) is null) return Results.NotFound();

        var ownerId = await db.Circles.Where(c => c.Id == id).Select(c => c.OwnerUserId).SingleAsync(ct);
        return Results.Ok(await ConsentService.GetAsync(db, ownerId, ct));
    }

    /// <summary>The uid of the caller when they are an elder, null otherwise (see <see cref="Denied"/>).</summary>
    private static async Task<string?> RequireElderAsync(ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return null;
        var isElder = await db.Users.AnyAsync(u => u.Id == uid && u.Role == Role.Elder, ct);
        return isElder ? uid : null;
    }

    private static IResult Denied(ClaimsPrincipal principal) =>
        principal.Uid() is null
            ? Results.Unauthorized()
            : Results.Problem("Only the data owner can manage consents.", statusCode: StatusCodes.Status403Forbidden);
}
