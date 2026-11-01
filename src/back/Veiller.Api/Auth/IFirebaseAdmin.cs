namespace Veiller.Api.Auth;

public sealed record FirebaseUser(
    string Uid,
    string? DisplayName,
    string? Email,
    IReadOnlyDictionary<string, object> Claims);

public sealed class UserNotFoundException(string message) : Exception(message);

public sealed class EmailAlreadyExistsException(string message) : Exception(message);

/// <summary>Thin facade over the Firebase Admin SDK so the endpoints can be tested without Firebase.</summary>
public interface IFirebaseAdmin
{
    /// <exception cref="EmailAlreadyExistsException">The email is already registered.</exception>
    /// <exception cref="ArgumentException">The email or password is rejected by Firebase.</exception>
    Task<FirebaseUser> CreateUserAsync(string email, string password, string displayName, CancellationToken ct);

    /// <exception cref="UserNotFoundException">No user with this uid.</exception>
    Task<FirebaseUser> GetUserAsync(string uid, CancellationToken ct);

    Task SetCustomClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken ct);

    Task DeleteUserAsync(string uid, CancellationToken ct);

    Task RevokeRefreshTokensAsync(string uid, CancellationToken ct);
}
