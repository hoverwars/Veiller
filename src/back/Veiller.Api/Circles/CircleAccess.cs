using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles;

/// <summary>Reusable "is this user a member of circle {id}?" check. Non-members get null, which callers map to 404.</summary>
public static class CircleAccess
{
    public static Task<CircleMember?> FindMembershipAsync(
        this VeillerDbContext db, Guid circleId, string userId, CancellationToken ct) =>
        db.CircleMembers.AsNoTracking().SingleOrDefaultAsync(m => m.CircleId == circleId && m.UserId == userId, ct);
}
