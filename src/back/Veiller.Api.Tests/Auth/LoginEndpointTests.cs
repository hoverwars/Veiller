using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Veiller.Api.Auth;
using Veiller.Api.Tests.Infrastructure;

namespace Veiller.Api.Tests.Auth;

public sealed class LoginEndpointTests : IDisposable
{
    private const string Url = "/api/auth/login";

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;

    public LoginEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private FirebaseUser AddUser(string uid = "uid-1", params (string Key, object Value)[] claims)
    {
        var user = new FirebaseUser(uid, "Jane Doe", "jane@example.com",
            claims.ToDictionary(c => c.Key, c => c.Value));
        _factory.Admin.Users[uid] = user;
        return user;
    }

    private void SignInSucceedsFor(string uid) =>
        _factory.Identity.OnSignIn = (_, _) => new FirebaseTokens(uid, "id-token", "refresh-token", 3600);

    private void SignInFailsWith(AuthFailure failure) =>
        _factory.Identity.OnSignIn = (_, _) => throw new AuthFailureException(failure, "boom");

    private Task<HttpResponseMessage> PostJsonAsync(string json, string mediaType = "application/json") =>
        _client.PostAsync(Url, new StringContent(json, Encoding.UTF8, mediaType));

    // ---- Success ----

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_and_session()
    {
        AddUser("uid-1");
        SignInSucceedsFor("uid-1");

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("id-token", body.GetProperty("idToken").GetString());
        Assert.Equal("refresh-token", body.GetProperty("refreshToken").GetString());
        Assert.Equal(3600, body.GetProperty("expiresIn").GetInt32());
        var user = body.GetProperty("user");
        Assert.Equal("uid-1", user.GetProperty("id").GetString());
        Assert.Equal("Jane Doe", user.GetProperty("fullName").GetString());
        Assert.Equal("jane@example.com", user.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("elder", "Elder")]
    [InlineData("caregiver", "Caregiver")]
    [InlineData("something-else", "Caregiver")]
    public async Task Login_maps_role_claim(string claim, string expectedRole)
    {
        AddUser("uid-1", ("role", claim));
        SignInSucceedsFor("uid-1");

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedRole, body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_without_role_claim_defaults_to_caregiver_and_not_onboarded()
    {
        AddUser("uid-1");
        SignInSucceedsFor("uid-1");

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        var user = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        Assert.Equal("Caregiver", user.GetProperty("role").GetString());
        Assert.False(user.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task Login_reads_onboarding_flag_from_claims()
    {
        AddUser("uid-1", ("onboardingCompleted", true));
        SignInSucceedsFor("uid-1");

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        var user = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        Assert.True(user.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task Login_trims_the_email_but_not_the_password()
    {
        AddUser("uid-1");
        SignInSucceedsFor("uid-1");

        await _client.PostAsJsonAsync(Url, new { email = "  jane@example.com  ", password = " secret1 " });

        var call = Assert.Single(_factory.Identity.SignInCalls);
        Assert.Equal("jane@example.com", call.Email);
        Assert.Equal(" secret1 ", call.Password);
    }

    // ---- Input validation (400, Firebase never called) ----

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"password":"secret1"}""")]
    [InlineData("""{"email":"jane@example.com"}""")]
    [InlineData("""{"email":null,"password":"secret1"}""")]
    [InlineData("""{"email":"jane@example.com","password":null}""")]
    [InlineData("""{"email":"","password":"secret1"}""")]
    [InlineData("""{"email":"jane@example.com","password":""}""")]
    [InlineData("""{"email":"   ","password":"secret1"}""")]
    [InlineData("""{"email":"jane@example.com","password":"   "}""")]
    public async Task Login_with_missing_or_empty_fields_returns_400(string json)
    {
        var response = await PostJsonAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Identity.SignInCalls);
    }

    [Fact]
    public async Task Login_validation_error_is_a_problem_details_naming_the_fields()
    {
        var response = await PostJsonAsync("{}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"email\":")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"email":123,"password":"secret1"}""")]
    public async Task Login_with_malformed_body_returns_400(string json)
    {
        var response = await PostJsonAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Identity.SignInCalls);
    }

    [Fact]
    public async Task Login_with_non_json_content_type_returns_415()
    {
        var response = await PostJsonAsync("email=a&password=b", "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_http_method_returns_405()
    {
        var response = await _client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public async Task Login_does_not_validate_the_email_format_and_defers_to_firebase(string email)
    {
        // LoginRequest only has [Required]; documents the current behaviour.
        SignInFailsWith(AuthFailure.InvalidCredentials);

        var response = await _client.PostAsJsonAsync(Url, new { email, password = "secret1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(email, Assert.Single(_factory.Identity.SignInCalls).Email);
    }

    // ---- Firebase failures ----

    [Fact]
    public async Task Login_with_bad_credentials_returns_401_with_generic_message()
    {
        SignInFailsWith(AuthFailure.InvalidCredentials);

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Incorrect email or password.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_with_too_many_attempts_returns_429()
    {
        SignInFailsWith(AuthFailure.TooManyAttempts);

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_when_firebase_fails_unexpectedly_returns_502()
    {
        SignInFailsWith(AuthFailure.Unknown);

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Login_when_user_record_is_missing_after_sign_in_throws()
    {
        // Current behaviour: UserNotFoundException is not handled by LoginAsync, so the request fails with a 500.
        // Likely worth mapping to a 401 instead; kept as-is until decided.
        SignInSucceedsFor("ghost");

        var response = await _client.PostAsJsonAsync(Url, new { email = "jane@example.com", password = "secret1" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
