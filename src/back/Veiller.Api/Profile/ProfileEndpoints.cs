using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Profile;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile").RequireAuthorization();

        group.MapGet("/", GetAsync)
            .WithDescription("The current user's profile.");
        group.MapPatch("/", UpdateAsync)
            .WithDescription("Partial update of name, phone, relation and birth date. Null/absent fields are untouched; an empty phone or relation clears it.");
        group.MapDelete("/", DeleteAsync)
            .WithDescription("Deletes the account. A caregiver leaves their circles; an elder's circle and health data are erased. Safe to retry.");
        group.MapGet("/export", ExportAsync)
            .WithDescription("Exports the personal data stored about the current user (right of access).");

        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == uid, ct);
        return user is null ? Results.NotFound() : Results.Ok(ProfileService.ToDto(user));
    }

    private static async Task<IResult> UpdateAsync(
        UpdateProfileRequest request, ClaimsPrincipal principal, VeillerDbContext db, IFirebaseAdmin admin, ILoggerFactory loggers, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        if (request.FullName is null && request.Phone is null && request.Relation is null && request.BirthDate is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { [""] = ["At least one field must be provided."] });
        if (request.FullName is { } name && name.Trim().Length < 2)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["fullName"] = ["The full name must have at least 2 characters."] });

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Results.NotFound();

        var renamed = ProfileService.Apply(user, request);

        // Firebase first: if it fails nothing is saved, so a retry still sees the rename and re-syncs.
        if (renamed is not null)
        {
            try
            {
                await admin.UpdateDisplayNameAsync(uid, renamed, ct);
            }
            catch (UserNotFoundException)
            {
                // Identity already gone from Firebase: the local profile is still the source of truth.
            }
            catch (Exception ex)
            {
                loggers.CreateLogger("Veiller.Api.Profile").LogError(ex, "Display name of {Uid} could not be synced to Firebase", uid);
                return Results.Problem("The profile could not be updated, try again.", statusCode: StatusCodes.Status502BadGateway);
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ProfileService.ToDto(user));
    }

    private static async Task<IResult> DeleteAsync(
        ClaimsPrincipal principal, VeillerDbContext db, IFirebaseAdmin admin, ILoggerFactory loggers, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        var hadProfile = await ProfileService.DeleteAsync(db, uid, ct);
        try
        {
            await admin.DeleteUserAsync(uid, ct);
        }
        catch (UserNotFoundException)
        {
            // Nothing in Firebase either: only a completely unknown account is reported as missing.
            if (!hadProfile) return Results.NotFound();
        }
        catch (Exception ex)
        {
            // Local data is already erased; the call can be retried and will finish the Firebase side.
            loggers.CreateLogger("Veiller.Api.Profile").LogError(ex, "Local data of {Uid} deleted but the Firebase account could not be", uid);
            return Results.Problem("The account could not be fully deleted, try again.", statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ExportAsync(
        ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == uid, ct);
        return user is null ? Results.NotFound() : Results.Ok(await ProfileService.ExportAsync(db, user, clock, ct));
    }
}
