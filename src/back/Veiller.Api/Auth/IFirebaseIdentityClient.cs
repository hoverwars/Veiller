namespace Veiller.Api.Auth;

public interface IFirebaseIdentityClient
{
    Task<FirebaseTokens> SignInWithPasswordAsync(string email, string password, CancellationToken ct);

    Task<FirebaseTokens> RefreshAsync(string refreshToken, CancellationToken ct);
}
