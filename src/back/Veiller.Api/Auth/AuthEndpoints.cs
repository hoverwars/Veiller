using System.Security.Claims;

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
        IFirebaseAdmin admin,
        IFirebaseIdentityClient identity,
        ILogger<FirebaseIdentityClient> logger,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        FirebaseUser created;
        try
        {
            created = await admin.CreateUserAsync(email, request.Password, request.FullName.Trim(), ct);
        }
        catch (EmailAlreadyExistsException)
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
            await admin.SetCustomClaimsAsync(created.Uid, new Dictionary<string, object>
            {
                [RoleClaim] = ToClaim(request.Role),
                [OnboardingClaim] = false,
            }, ct);

            var tokens = await identity.SignInWithPasswordAsync(email, request.Password, ct);
            var user = new UserDto(created.Uid, created.DisplayName ?? "", email, request.Role, false);
            return Results.Created("/api/auth/me",
                new AuthResponse(user, tokens.IdToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (Exception ex)
        {
            // Do not leave an account without a role behind.
            logger.LogError(ex, "Registration failed after account creation, rolling back {Uid}", created.Uid);
            await admin.DeleteUserAsync(created.Uid, CancellationToken.None);
            return Results.Problem("Could not complete the registration.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IFirebaseAdmin admin,
        IFirebaseIdentityClient identity,
        CancellationToken ct)
    {
        try
        {
            var tokens = await identity.SignInWithPasswordAsync(request.Email.Trim(), request.Password, ct);
            var user = await admin.GetUserAsync(tokens.Uid, ct);
            return Results.Ok(new AuthResponse(ToDto(user), tokens.IdToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (AuthFailureException ex)
        {
            return FailureToProblem(ex, "Incorrect email or password.");
        }
        catch (UserNotFoundException)
        {
            return Results.Problem("Incorrect email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IFirebaseIdentityClient identity,
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

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, IFirebaseAdmin admin, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(principal, admin, ct);
        return user is null ? Results.Unauthorized() : Results.Ok(ToDto(user));
    }

    private static async Task<IResult> CompleteOnboardingAsync(
        ClaimsPrincipal principal,
        IFirebaseAdmin admin,
        CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(principal, admin, ct);
        if (user is null) return Results.Unauthorized();

        var claims = new Dictionary<string, object>(user.Claims) { [OnboardingClaim] = true };
        await admin.SetCustomClaimsAsync(user.Uid, claims, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(ClaimsPrincipal principal, IFirebaseAdmin admin, CancellationToken ct)
    {
        var uid = UidOf(principal);
        if (uid is null) return Results.Unauthorized();

        await admin.RevokeRefreshTokensAsync(uid, ct);
        return Results.NoContent();
    }

    private static string? UidOf(ClaimsPrincipal principal) =>
        principal.FindFirstValue("user_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<FirebaseUser?> GetCurrentUserAsync(ClaimsPrincipal principal, IFirebaseAdmin admin, CancellationToken ct)
    {
        var uid = UidOf(principal);
        if (uid is null) return null;

        try
        {
            return await admin.GetUserAsync(uid, ct);
        }
        catch (UserNotFoundException)
        {
            return null;
        }
    }

    private static UserDto ToDto(FirebaseUser user)
    {
        var role = user.Claims.TryGetValue(RoleClaim, out var r) && r?.ToString() == "elder"
            ? Role.Elder
            : Role.Caregiver;
        var onboarded = user.Claims.TryGetValue(OnboardingClaim, out var o) && o is true;
        return new UserDto(user.Uid, user.DisplayName ?? "", user.Email ?? "", role, onboarded);
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
