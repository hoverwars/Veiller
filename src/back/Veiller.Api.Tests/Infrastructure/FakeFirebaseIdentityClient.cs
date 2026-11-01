using Veiller.Api.Auth;

namespace Veiller.Api.Tests.Infrastructure;

public sealed class FakeFirebaseIdentityClient : IFirebaseIdentityClient
{
    public List<(string Email, string Password)> SignInCalls { get; } = [];

    /// <summary>Result of the next sign-in. Defaults to a failure so a test never passes by accident.</summary>
    public Func<string, string, FirebaseTokens> OnSignIn { get; set; } =
        (_, _) => throw new AuthFailureException(AuthFailure.InvalidCredentials, "not configured");

    public Task<FirebaseTokens> SignInWithPasswordAsync(string email, string password, CancellationToken ct)
    {
        SignInCalls.Add((email, password));
        return Task.FromResult(OnSignIn(email, password));
    }

    public Task<FirebaseTokens> RefreshAsync(string refreshToken, CancellationToken ct) =>
        throw new NotSupportedException("Not used by the login tests.");
}
