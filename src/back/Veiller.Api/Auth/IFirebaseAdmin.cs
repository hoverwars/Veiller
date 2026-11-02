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

    Task UpdateDisplayNameAsync(string uid, string displayName, CancellationToken ct);

    /// <exception cref="EmailAlreadyExistsException">The email belongs to another account.</exception>
    /// <exception cref="UserNotFoundException">No user with this uid.</exception>
    Task UpdateEmailAsync(string uid, string email, CancellationToken ct);

    /// <exception cref="ArgumentException">The password is rejected by Firebase.</exception>
    /// <exception cref="UserNotFoundException">No user with this uid.</exception>
    Task UpdatePasswordAsync(string uid, string password, CancellationToken ct);

    /// <exception cref="UserNotFoundException">No user with this uid.</exception>
    Task DeleteUserAsync(string uid, CancellationToken ct);

    Task RevokeRefreshTokensAsync(string uid, CancellationToken ct);
}
