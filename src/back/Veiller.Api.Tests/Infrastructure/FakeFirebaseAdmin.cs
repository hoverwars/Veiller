using Veiller.Api.Auth;

namespace Veiller.Api.Tests.Infrastructure;

public sealed class FakeFirebaseAdmin : IFirebaseAdmin
{
    public Dictionary<string, FirebaseUser> Users { get; } = [];

    public Task<FirebaseUser> CreateUserAsync(string email, string password, string displayName, CancellationToken ct)
    {
        if (Users.Values.Any(u => u.Email == email))
            throw new EmailAlreadyExistsException(email);

        var user = new FirebaseUser(Guid.NewGuid().ToString("N"), displayName, email, new Dictionary<string, object>());
        Users[user.Uid] = user;
        return Task.FromResult(user);
    }

    public Task<FirebaseUser> GetUserAsync(string uid, CancellationToken ct) =>
        Users.TryGetValue(uid, out var user)
            ? Task.FromResult(user)
            : throw new UserNotFoundException(uid);

    public Task SetCustomClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken ct)
    {
        Users[uid] = Users[uid] with { Claims = new Dictionary<string, object>(claims) };
        return Task.CompletedTask;
    }

    /// <summary>When set, UpdateDisplayNameAsync throws it (simulates Firebase being unreachable).</summary>
    public Exception? UpdateFailure { get; set; }

    public Task UpdateDisplayNameAsync(string uid, string displayName, CancellationToken ct)
    {
        if (UpdateFailure is not null) throw UpdateFailure;
        Users[uid] = Users[uid] with { DisplayName = displayName };
        return Task.CompletedTask;
    }

    /// <summary>When set, UpdateEmailAsync and UpdatePasswordAsync throw it (simulates Firebase being unreachable).</summary>
    public Exception? CredentialsFailure { get; set; }

    public Dictionary<string, string> Passwords { get; } = [];

    public List<string> RevokedUids { get; } = [];

    public Task UpdateEmailAsync(string uid, string email, CancellationToken ct)
    {
        if (CredentialsFailure is not null) throw CredentialsFailure;
        if (!Users.TryGetValue(uid, out var user)) throw new UserNotFoundException(uid);
        if (Users.Values.Any(u => u.Uid != uid && u.Email == email)) throw new EmailAlreadyExistsException(email);
        Users[uid] = user with { Email = email };
        return Task.CompletedTask;
    }

    public Task UpdatePasswordAsync(string uid, string password, CancellationToken ct)
    {
        if (CredentialsFailure is not null) throw CredentialsFailure;
        if (!Users.ContainsKey(uid)) throw new UserNotFoundException(uid);
        Passwords[uid] = password;
        return Task.CompletedTask;
    }

    /// <summary>When set, DeleteUserAsync throws it (simulates Firebase being unreachable).</summary>
    public Exception? DeleteFailure { get; set; }

    public Task DeleteUserAsync(string uid, CancellationToken ct)
    {
        if (DeleteFailure is not null) throw DeleteFailure;
        if (!Users.Remove(uid)) throw new UserNotFoundException(uid);
        return Task.CompletedTask;
    }

    public Task RevokeRefreshTokensAsync(string uid, CancellationToken ct)
    {
        RevokedUids.Add(uid);
        return Task.CompletedTask;
    }
}
