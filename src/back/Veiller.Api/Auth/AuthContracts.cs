using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Veiller.Api.Auth;

[JsonConverter(typeof(JsonStringEnumConverter<Role>))]
public enum Role
{
    Caregiver,
    Elder,
}

public sealed record RegisterRequest(
    [property: Required, StringLength(100, MinimumLength = 2)] string FullName,
    [property: Required, EmailAddress] string Email,
    [property: Required, StringLength(128, MinimumLength = 6)] string Password,
    Role Role);

public sealed record LoginRequest(
    [property: Required] string Email,
    [property: Required] string Password);

public sealed record RefreshRequest([property: Required] string RefreshToken);

public sealed record UserDto(string Id, string FullName, string Email, Role Role, bool OnboardingCompleted);

public sealed record AuthResponse(UserDto User, string IdToken, string RefreshToken, int ExpiresIn);

public sealed record TokenResponse(string IdToken, string RefreshToken, int ExpiresIn);
