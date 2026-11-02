using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Profile;

public sealed class CredentialsEndpointTests : IDisposable
{
    private const string Uid = "user-1";
    private const string Password = "current-pass";

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CredentialsEndpointTests()
    {
        _client = _factory.CreateClient();
        Seed(Uid, "user-1@example.com").GetAwaiter().GetResult();
        // The fake identity provider accepts only the current password of the seeded user.
        _factory.Identity.OnSignIn = (email, password) => password == Password
            ? new FirebaseTokens(Uid, "id", "refresh", 3600)
            : throw new AuthFailureException(AuthFailure.InvalidCredentials, "bad password");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task Seed(string uid, string email)
    {
        using var _ = _factory.CreateDbScope(out var db);
        db.Users.Add(new User { Id = uid, Email = email, FullName = $"Name {uid}", Role = AuthRole.Caregiver, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        _factory.Admin.Users[uid] = new FirebaseUser(uid, $"Name {uid}", email, new Dictionary<string, object>());
    }

    private Task<HttpResponseMessage> Put(string path, string? uid, string? json)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _client.SendAsync(request, Ct);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);

    private Task<HttpResponseMessage> ChangeEmail(string? uid, object body) => Put("/api/profile/email", uid, Json(body));

    private Task<HttpResponseMessage> ChangePassword(string? uid, object body) => Put("/api/profile/password", uid, Json(body));

    private async Task<string> StoredEmailAsync()
    {
        using var _ = _factory.CreateDbScope(out var db);
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == Uid, Ct)).Email;
    }

    // ---- Email ----

    [Fact]
    public async Task Email_is_changed_in_firebase_and_locally_and_normalized()
    {
        var response = await ChangeEmail(Uid, new { newEmail = "  New.Address@Example.com ", currentPassword = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("new.address@example.com", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("email").GetString());
        Assert.Equal("new.address@example.com", await StoredEmailAsync());
        Assert.Equal("new.address@example.com", _factory.Admin.Users[Uid].Email);
    }

    [Fact]
    public async Task The_current_password_is_checked_against_the_current_email()
    {
        await ChangeEmail(Uid, new { newEmail = "new@example.com", currentPassword = Password });

        Assert.Equal([("user-1@example.com", Password)], _factory.Identity.SignInCalls);
    }

    [Fact]
    public async Task A_wrong_password_is_403_and_nothing_changes()
    {
        var response = await ChangeEmail(Uid, new { newEmail = "new@example.com", currentPassword = "nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("user-1@example.com", await StoredEmailAsync());
        Assert.Equal("user-1@example.com", _factory.Admin.Users[Uid].Email);
    }

    [Fact]
    public async Task An_email_used_by_another_account_is_409()
    {
        await Seed("user-2", "taken@example.com");

        var response = await ChangeEmail(Uid, new { newEmail = "Taken@example.com", currentPassword = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user-1@example.com", await StoredEmailAsync());
    }

    [Fact]
    public async Task Firebase_rejecting_the_email_as_taken_is_409_too()
    {
        // Known to Firebase only (e.g. an account not yet in the local database).
        _factory.Admin.Users["ghost"] = new FirebaseUser("ghost", "Ghost", "ghost@example.com", new Dictionary<string, object>());

        var response = await ChangeEmail(Uid, new { newEmail = "ghost@example.com", currentPassword = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user-1@example.com", await StoredEmailAsync());
    }

    [Fact]
    public async Task Keeping_the_same_email_is_400()
    {
        var response = await ChangeEmail(Uid, new { newEmail = "USER-1@example.com", currentPassword = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Identity.SignInCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public async Task An_invalid_email_is_400(string email)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeEmail(Uid, new { newEmail = email, currentPassword = Password })).StatusCode);
    }

    [Fact]
    public async Task A_missing_field_or_malformed_body_is_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeEmail(Uid, new { newEmail = "new@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeEmail(Uid, new { currentPassword = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put("/api/profile/email", Uid, "{nope")).StatusCode);
    }

    [Fact]
    public async Task Too_many_password_attempts_is_429()
    {
        _factory.Identity.OnSignIn = (_, _) => throw new AuthFailureException(AuthFailure.TooManyAttempts, "slow down");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ChangeEmail(Uid, new { newEmail = "new@example.com", currentPassword = Password })).StatusCode);
    }

    [Fact]
    public async Task Firebase_failure_on_email_is_502_and_nothing_is_saved()
    {
        _factory.Admin.CredentialsFailure = new HttpRequestException("down");

        var response = await ChangeEmail(Uid, new { newEmail = "new@example.com", currentPassword = Password });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("user-1@example.com", await StoredEmailAsync());
    }

    [Fact]
    public async Task Email_change_requires_authentication_and_a_profile()
    {
        var body = new { newEmail = "new@example.com", currentPassword = Password };

        Assert.Equal(HttpStatusCode.Unauthorized, (await ChangeEmail(null, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeEmail("unknown", body)).StatusCode);
    }

    // ---- Password ----

    [Fact]
    public async Task Password_is_changed_and_every_session_revoked()
    {
        var response = await ChangePassword(Uid, new { currentPassword = Password, newPassword = "brand-new-pass" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("brand-new-pass", _factory.Admin.Passwords[Uid]);
        Assert.Equal([Uid], _factory.Admin.RevokedUids);
    }

    [Fact]
    public async Task Password_change_with_a_wrong_current_password_is_403()
    {
        var response = await ChangePassword(Uid, new { currentPassword = "nope", newPassword = "brand-new-pass" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Admin.Passwords);
        Assert.Empty(_factory.Admin.RevokedUids);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task A_too_short_new_password_is_400(string newPassword)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePassword(Uid, new { currentPassword = Password, newPassword })).StatusCode);
        Assert.Empty(_factory.Admin.Passwords);
    }

    [Fact]
    public async Task A_too_long_new_password_is_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePassword(Uid, new { currentPassword = Password, newPassword = new string('x', 129) })).StatusCode);
    }

    [Fact]
    public async Task Reusing_the_current_password_is_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePassword(Uid, new { currentPassword = Password, newPassword = Password })).StatusCode);
        Assert.Empty(_factory.Identity.SignInCalls);
    }

    [Fact]
    public async Task A_missing_password_field_is_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePassword(Uid, new { newPassword = "brand-new-pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePassword(Uid, new { currentPassword = Password })).StatusCode);
    }

    [Fact]
    public async Task Firebase_failure_on_password_is_502()
    {
        _factory.Admin.CredentialsFailure = new HttpRequestException("down");

        var response = await ChangePassword(Uid, new { currentPassword = Password, newPassword = "brand-new-pass" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Firebase_refusing_the_password_is_400()
    {
        _factory.Admin.CredentialsFailure = new ArgumentException("weak");

        var response = await ChangePassword(Uid, new { currentPassword = Password, newPassword = "brand-new-pass" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_change_requires_authentication_and_a_profile()
    {
        var body = new { currentPassword = Password, newPassword = "brand-new-pass" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await ChangePassword(null, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangePassword("unknown", body)).StatusCode);
    }
}
