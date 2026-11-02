using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Profile;

/// <summary>Both changes ask for the current password again: a stolen session alone must not be enough to take over the account.</summary>
public sealed record ChangeEmailRequest(
    [property: Required, EmailAddress, StringLength(254)] string NewEmail,
    [property: Required] string CurrentPassword);

public sealed record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required, StringLength(128, MinimumLength = 6)] string NewPassword);

public static class CredentialsEndpoints
{
    public static IEndpointRouteBuilder MapCredentialsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile").RequireAuthorization();

        group.MapPut("/email", ChangeEmailAsync)
            .WithDescription("Changes the sign-in email. Requires the current password. 403 when it is wrong, 409 when the email is taken.");
        group.MapPut("/password", ChangePasswordAsync)
            .WithDescription("Changes the password. Requires the current password. Every session is revoked: the client must sign in again.");
        return app;
    }

    private static async Task<IResult> ChangeEmailAsync(
        ChangeEmailRequest request, ClaimsPrincipal principal, VeillerDbContext db, IFirebaseAdmin admin,
        IFirebaseIdentityClient identity, ILoggerFactory loggers, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Results.NotFound();

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();
        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["newEmail"] = ["This is already your email."] });
        if (await db.Users.AnyAsync(u => u.Email == newEmail && u.Id != uid, ct)) return EmailTaken();

        if (await CheckPasswordAsync(identity, user.Email, request.CurrentPassword, ct) is { } denied) return denied;

        try
        {
            // Firebase first: if it fails nothing is saved locally, and a retry starts over.
            await admin.UpdateEmailAsync(uid, newEmail, ct);
        }
        catch (EmailAlreadyExistsException)
        {
            return EmailTaken();
        }
        catch (UserNotFoundException)
        {
            return Results.NotFound();
        }
        catch (Exception ex)
        {
            loggers.CreateLogger("Veiller.Api.Profile").LogError(ex, "Email of {Uid} could not be changed in Firebase", uid);
            return Results.Problem("The email could not be changed, try again.", statusCode: StatusCodes.Status502BadGateway);
        }

        user.Email = newEmail;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ProfileService.ToDto(user));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request, ClaimsPrincipal principal, VeillerDbContext db, IFirebaseAdmin admin,
        IFirebaseIdentityClient identity, ILoggerFactory loggers, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Results.NotFound();

        if (request.NewPassword == request.CurrentPassword)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["The new password must differ from the current one."] });
        if (await CheckPasswordAsync(identity, user.Email, request.CurrentPassword, ct) is { } denied) return denied;

        try
        {
            await admin.UpdatePasswordAsync(uid, request.NewPassword, ct);
            await admin.RevokeRefreshTokensAsync(uid, ct);
        }
        catch (ArgumentException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["This password is not accepted."] });
        }
        catch (UserNotFoundException)
        {
            return Results.NotFound();
        }
        catch (Exception ex)
        {
            loggers.CreateLogger("Veiller.Api.Profile").LogError(ex, "Password of {Uid} could not be changed in Firebase", uid);
            return Results.Problem("The password could not be changed, try again.", statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.NoContent();
    }

    /// <summary>Re-authenticates the caller with their current password; null when it is right, otherwise the response to return.</summary>
    private static async Task<IResult?> CheckPasswordAsync(IFirebaseIdentityClient identity, string email, string password, CancellationToken ct)
    {
        try
        {
            await identity.SignInWithPasswordAsync(email, password, ct);
            return null;
        }
        catch (AuthFailureException e) when (e.Failure == AuthFailure.InvalidCredentials)
        {
            // 403, not 401: the token is fine, the client must not treat it as an expired session.
            return Results.Problem("The current password is incorrect.", statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthFailureException e) when (e.Failure == AuthFailure.TooManyAttempts)
        {
            return Results.Problem("Too many attempts, try again later.", statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (AuthFailureException)
        {
            return Results.Problem("The password could not be checked, try again.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult EmailTaken() => Results.Problem("This email is already used by another account.", statusCode: StatusCodes.Status409Conflict);
}
