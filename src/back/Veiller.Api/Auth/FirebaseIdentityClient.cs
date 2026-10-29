using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Veiller.Api.Auth;

public enum AuthFailure
{
    InvalidCredentials,
    TooManyAttempts,
    InvalidRefreshToken,
    Unknown,
}

public sealed class AuthFailureException(AuthFailure failure, string message) : Exception(message)
{
    public AuthFailure Failure { get; } = failure;
}

public sealed record FirebaseTokens(string Uid, string IdToken, string RefreshToken, int ExpiresIn);

/// <summary>Client for the Firebase Auth REST API (Identity Toolkit and Secure Token).</summary>
public sealed class FirebaseIdentityClient(HttpClient http, IOptions<FirebaseOptions> options)
{
    private const string SignInUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword";
    private const string RefreshUrl = "https://securetoken.googleapis.com/v1/token";

    private readonly string _apiKey = options.Value.WebApiKey;

    public async Task<FirebaseTokens> SignInWithPasswordAsync(string email, string password, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"{SignInUrl}?key={Uri.EscapeDataString(_apiKey)}",
            new { email, password, returnSecureToken = true },
            ct);

        if (!response.IsSuccessStatusCode)
            throw await ToFailureAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<SignInResponse>(ct)
                   ?? throw new AuthFailureException(AuthFailure.Unknown, "Empty response from Firebase");
        return new FirebaseTokens(body.LocalId, body.IdToken, body.RefreshToken, int.Parse(body.ExpiresIn));
    }

    public async Task<FirebaseTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"{RefreshUrl}?key={Uri.EscapeDataString(_apiKey)}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            }),
            ct);

        if (!response.IsSuccessStatusCode)
            throw await ToFailureAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<RefreshResponse>(ct)
                   ?? throw new AuthFailureException(AuthFailure.Unknown, "Empty response from Firebase");
        return new FirebaseTokens(body.UserId, body.IdToken, body.RefreshToken, int.Parse(body.ExpiresIn));
    }

    private static async Task<AuthFailureException> ToFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? code = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            code = error?.Error?.Message;
        }
        catch (JsonException)
        {
        }

        // Firebase may append details after the code ("TOO_MANY_ATTEMPTS_TRY_LATER : ...").
        var failure = code?.Split(' ', 2)[0] switch
        {
            "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND" or "USER_DISABLED"
                => AuthFailure.InvalidCredentials,
            "TOO_MANY_ATTEMPTS_TRY_LATER" => AuthFailure.TooManyAttempts,
            "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED" or "USER_NOT_FOUND" or "INVALID_GRANT_TYPE"
                or "MISSING_REFRESH_TOKEN" => AuthFailure.InvalidRefreshToken,
            _ => AuthFailure.Unknown,
        };
        return new AuthFailureException(failure, $"Firebase error: {code ?? response.StatusCode.ToString()}");
    }

    private sealed record SignInResponse(string LocalId, string IdToken, string RefreshToken, string ExpiresIn);

    private sealed record RefreshResponse(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] string ExpiresIn);

    private sealed record ErrorResponse(ErrorBody? Error);

    private sealed record ErrorBody(string? Message);
}
