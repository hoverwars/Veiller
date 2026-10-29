using System.Security.Claims;
using FirebaseAdmin.Auth;

namespace Veiller.Api.Auth;

public static class AuthEndpoints
{
    private const string RoleClaim = "role";
    private const string OnboardingClaim = "onboardingCompleted";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithDescription("Creates an account (email + password) with its role and returns the session tokens.");
        group.MapPost("/login", LoginAsync)
            .WithDescription("Signs in with email and password.");
        group.MapPost("/refresh", RefreshAsync)
            .WithDescription("Exchanges a refresh token for a new ID token.");

        var authed = group.MapGroup("").RequireAuthorization();
        authed.MapGet("/me", MeAsync)
            .WithDescription("Returns the current user, read from Firebase (always up to date).");
        authed.MapPost("/me/onboarding", CompleteOnboardingAsync)
            .WithDescription("Marks the onboarding as completed. Refresh the token to see it in the JWT claims.");
        authed.MapPost("/logout", LogoutAsync)
            .WithDescription("Revokes the refresh tokens of the user on every device.");

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        FirebaseAuth auth,
        FirebaseIdentityClient identity,
        ILogger<FirebaseIdentityClient> logger,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        UserRecord created;
        try
        {
            created = await auth.CreateUserAsync(new UserRecordArgs
            {
                Email = email,
                Password = request.Password,
                DisplayName = request.FullName.Trim(),
            }, ct);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            return Results.Problem("An account with this email already exists.", statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException)
        {
            return Results.Problem("Invalid email or password.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            // Claims go in before the first sign-in so the very first token already carries the role.
            await auth.SetCustomUserClaimsAsync(created.Uid, new Dictionary<string, object>
            {
                [RoleClaim] = ToClaim(request.Role),
                [OnboardingClaim] = false,
            }, ct);

            var tokens = await identity.SignInWithPasswordAsync(email, request.Password, ct);
            var user = new UserDto(created.Uid, created.DisplayName, email, request.Role, false);
            return Results.Created("/api/auth/me",
                new AuthResponse(user, tokens.IdToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (Exception ex)
        {
            // Do not leave an account without a role behind.
            logger.LogError(ex, "Registration failed after account creation, rolling back {Uid}", created.Uid);
            await auth.DeleteUserAsync(created.Uid, CancellationToken.None);
            return Results.Problem("Could not complete the registration.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        FirebaseAuth auth,
        FirebaseIdentityClient identity,
        CancellationToken ct)
    {
        try
        {
            var tokens = await identity.SignInWithPasswordAsync(request.Email.Trim(), request.Password, ct);
            var record = await auth.GetUserAsync(tokens.Uid, ct);
            return Results.Ok(new AuthResponse(ToDto(record), tokens.IdToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (AuthFailureException ex)
        {
            return FailureToProblem(ex, "Incorrect email or password.");
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        FirebaseIdentityClient identity,
        CancellationToken ct)
    {
        try
        {
            var tokens = await identity.RefreshAsync(request.RefreshToken, ct);
            return Results.Ok(new TokenResponse(tokens.IdToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (AuthFailureException ex)
        {
            return FailureToProblem(ex, "Invalid or expired refresh token.");
        }
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, FirebaseAuth auth, CancellationToken ct)
    {
        var record = await GetCurrentUserAsync(principal, auth, ct);
        return record is null ? Results.Unauthorized() : Results.Ok(ToDto(record));
    }

    private static async Task<IResult> CompleteOnboardingAsync(
        ClaimsPrincipal principal,
        FirebaseAuth auth,
        CancellationToken ct)
    {
        var record = await GetCurrentUserAsync(principal, auth, ct);
        if (record is null) return Results.Unauthorized();

        var claims = new Dictionary<string, object>(record.CustomClaims) { [OnboardingClaim] = true };
        await auth.SetCustomUserClaimsAsync(record.Uid, claims, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(ClaimsPrincipal principal, FirebaseAuth auth, CancellationToken ct)
    {
        var uid = UidOf(principal);
        if (uid is null) return Results.Unauthorized();

        await auth.RevokeRefreshTokensAsync(uid, ct);
        return Results.NoContent();
    }

    private static string? UidOf(ClaimsPrincipal principal) =>
        principal.FindFirstValue("user_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<UserRecord?> GetCurrentUserAsync(ClaimsPrincipal principal, FirebaseAuth auth, CancellationToken ct)
    {
        var uid = UidOf(principal);
        if (uid is null) return null;

        try
        {
            return await auth.GetUserAsync(uid, ct);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            return null;
        }
    }

    private static UserDto ToDto(UserRecord record)
    {
        var role = record.CustomClaims.TryGetValue(RoleClaim, out var r) && r?.ToString() == "elder"
            ? Role.Elder
            : Role.Caregiver;
        var onboarded = record.CustomClaims.TryGetValue(OnboardingClaim, out var o) && o is true;
        return new UserDto(record.Uid, record.DisplayName ?? "", record.Email ?? "", role, onboarded);
    }

    private static string ToClaim(Role role) => role == Role.Elder ? "elder" : "caregiver";

    private static IResult FailureToProblem(AuthFailureException ex, string unauthorizedMessage) => ex.Failure switch
    {
        AuthFailure.TooManyAttempts => Results.Problem("Too many attempts, try again later.",
            statusCode: StatusCodes.Status429TooManyRequests),
        AuthFailure.Unknown => Results.Problem("Authentication service unavailable.",
            statusCode: StatusCodes.Status502BadGateway),
        _ => Results.Problem(unauthorizedMessage, statusCode: StatusCodes.Status401Unauthorized),
    };
}
