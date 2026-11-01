using FirebaseAdmin.Auth;

namespace Veiller.Api.Auth;

public sealed class FirebaseAdminFacade(FirebaseAuth auth) : IFirebaseAdmin
{
    public async Task<FirebaseUser> CreateUserAsync(string email, string password, string displayName, CancellationToken ct)
    {
        try
        {
            var record = await auth.CreateUserAsync(new UserRecordArgs
            {
                Email = email,
                Password = password,
                DisplayName = displayName,
            }, ct);
            return ToUser(record);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            throw new EmailAlreadyExistsException(ex.Message);
        }
    }

    public async Task<FirebaseUser> GetUserAsync(string uid, CancellationToken ct)
    {
        try
        {
            return ToUser(await auth.GetUserAsync(uid, ct));
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            throw new UserNotFoundException(ex.Message);
        }
    }

    public Task SetCustomClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken ct) =>
        auth.SetCustomUserClaimsAsync(uid, new Dictionary<string, object>(claims), ct);

    public Task DeleteUserAsync(string uid, CancellationToken ct) => auth.DeleteUserAsync(uid, ct);

    public Task RevokeRefreshTokensAsync(string uid, CancellationToken ct) => auth.RevokeRefreshTokensAsync(uid, ct);

    private static FirebaseUser ToUser(UserRecord record) =>
        new(record.Uid, record.DisplayName, record.Email, new Dictionary<string, object>(record.CustomClaims));
}
