using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Tests.Infrastructure;

namespace Veiller.Api.Tests.Auth;

public sealed class RegisterEndpointTests : IDisposable
{
    private const string Url = "/api/auth/register";

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;

    public RegisterEndpointTests()
    {
        _client = _factory.CreateClient();
        _factory.Identity.OnSignIn = (_, _) => new FirebaseTokens("uid", "id-token", "refresh-token", 3600);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object Valid(
        string fullName = "Jane Doe",
        string email = "Jane@Example.com",
        string password = "secret1",
        string role = "caregiver") => new { fullName, email, password, role };

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _client.PostAsJsonAsync(Url, body, TestContext.Current.CancellationToken);

    private Task<int> CountUsersAsync()
    {
        using var _ = _factory.CreateDbScope(out var db);
        return db.Users.CountAsync(TestContext.Current.CancellationToken);
    }

    // ---- Success ----

    [Theory]
    [InlineData("caregiver", Role.Caregiver)]
    [InlineData("elder", Role.Elder)]
    public async Task Register_with_valid_input_returns_201_and_stores_the_user(string role, Role expected)
    {
        var response = await PostAsync(Valid(role: role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var uid = Assert.Single(_factory.Admin.Users.Keys);

        using var _ = _factory.CreateDbScope(out var db);
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(uid, user.Id);
        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("Jane Doe", user.FullName);
        Assert.Equal(expected, user.Role);
        Assert.True(user.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    // ---- Input validation ----

    [Theory]
    [InlineData("")]
    [InlineData("J")]
    public async Task Register_with_bad_full_name_returns_400(string fullName)
    {
        var response = await PostAsync(Valid(fullName: fullName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
        Assert.Empty(_factory.Admin.Users);
    }

    [Fact]
    public async Task Register_with_too_long_full_name_returns_400()
    {
        var response = await PostAsync(Valid(fullName: new string('a', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Register_with_bad_email_returns_400(string email)
    {
        var response = await PostAsync(Valid(email: email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    public async Task Register_with_short_password_returns_400(string password)
    {
        var response = await PostAsync(Valid(password: password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Register_with_a_valid_birth_date_stores_it()
    {
        var response = await PostAsync(new
        {
            fullName = "Rosa Garcia", email = "rosa@example.com", password = "secret1", role = "elder",
            birthDate = "1945-03-12",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Equal(new DateOnly(1945, 3, 12), (await db.Users.SingleAsync(TestContext.Current.CancellationToken)).BirthDate);
    }

    [Theory]
    [InlineData("2999-01-01")]
    [InlineData("1800-01-01")]
    [InlineData("not-a-date")]
    public async Task Register_with_a_bad_birth_date_returns_400(string birthDate)
    {
        var response = await PostAsync(new
        {
            fullName = "Rosa Garcia", email = "rosa@example.com", password = "secret1", role = "elder", birthDate,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
        Assert.Empty(_factory.Admin.Users);
    }

    [Fact]
    public async Task Register_with_unknown_role_returns_400()
    {
        var response = await PostAsync(Valid(role: "admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Register_with_missing_fields_returns_400()
    {
        var response = await PostAsync(new { email = "jane@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_malformed_json_returns_400()
    {
        var response = await _client.PostAsync(Url,
            new StringContent("{ not json", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Conflicts and failures ----

    [Fact]
    public async Task Register_with_existing_email_returns_409_without_duplicating_the_user()
    {
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(Valid())).StatusCode);

        var response = await PostAsync(Valid(email: "JANE@example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
        Assert.Single(_factory.Admin.Users);
    }

    [Fact]
    public async Task Register_when_the_database_fails_returns_500_and_rolls_back_the_firebase_account()
    {
        using (var scope = _factory.CreateDbScope(out var db))
            await db.Database.ExecuteSqlRawAsync("DROP TABLE users", TestContext.Current.CancellationToken);

        var response = await PostAsync(Valid());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.Admin.Users);
    }

    [Fact]
    public async Task Register_when_the_rollback_also_fails_still_returns_a_500_problem()
    {
        using (var scope = _factory.CreateDbScope(out var db))
            await db.Database.ExecuteSqlRawAsync("DROP TABLE users", TestContext.Current.CancellationToken);
        _factory.Admin.DeleteFailure = new InvalidOperationException("firebase down");

        var response = await PostAsync(Valid());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
