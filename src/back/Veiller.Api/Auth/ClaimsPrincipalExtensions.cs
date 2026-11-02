using System.Security.Claims;

namespace Veiller.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Firebase uid of the caller, or null when the token carries none.</summary>
    public static string? Uid(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("user_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
