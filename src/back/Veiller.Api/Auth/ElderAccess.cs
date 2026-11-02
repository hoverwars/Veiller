using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;

namespace Veiller.Api.Auth;

/// <summary>Shared guard for endpoints reserved to elders (data owners). The role comes from the stored profile, like everywhere else.</summary>
public static class ElderAccess
{
    /// <summary>The uid of the caller when they are an elder, null otherwise (see <see cref="Denied"/>).</summary>
    public static async Task<string?> RequireElderAsync(ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return null;
        var isElder = await db.Users.AnyAsync(u => u.Id == uid && u.Role == Role.Elder, ct);
        return isElder ? uid : null;
    }

    /// <summary>401 when the token carries no uid, 403 (with <paramref name="message"/>) when the caller is not an elder.</summary>
    public static IResult Denied(ClaimsPrincipal principal, string message) =>
        principal.Uid() is null
            ? Results.Unauthorized()
            : Results.Problem(message, statusCode: StatusCodes.Status403Forbidden);
}
