using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles;

public enum RemoveMemberOutcome
{
    Removed,
    NotFound,
    Forbidden,
    CannotRemoveOwner,
}

public static class CircleService
{
    /// <summary>Creates the circle of an elder with the elder as data owner. Idempotent.</summary>
    public static async Task EnsureOwnCircleAsync(
        VeillerDbContext db, string ownerUserId, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Circles.AnyAsync(c => c.OwnerUserId == ownerUserId, ct)) return;

        var now = clock.GetUtcNow();
        var circle = new Circle { OwnerUserId = ownerUserId, CreatedAt = now };
        circle.Members.Add(new CircleMember
        {
            CircleId = circle.Id,
            UserId = ownerUserId,
            Role = MemberRole.DataOwner,
            JoinedAt = now,
        });
        db.Circles.Add(circle);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have created the circle first: that is fine, anything else is a real error.
            db.ChangeTracker.Clear();
            if (!await db.Circles.AsNoTracking().AnyAsync(c => c.OwnerUserId == ownerUserId, ct)) throw;
        }
    }

    public static async Task<RemoveMemberOutcome> RemoveMemberAsync(
        VeillerDbContext db, CircleMember actor, string targetUserId, CancellationToken ct)
    {
        var target = await db.CircleMembers.SingleOrDefaultAsync(
            m => m.CircleId == actor.CircleId && m.UserId == targetUserId, ct);
        if (target is null) return RemoveMemberOutcome.NotFound;
        if (target.Role == MemberRole.DataOwner) return RemoveMemberOutcome.CannotRemoveOwner;

        var isSelf = actor.UserId == targetUserId;
        var isPrivileged = actor.Role is MemberRole.Admin or MemberRole.DataOwner;
        if (!isSelf && !isPrivileged) return RemoveMemberOutcome.Forbidden;

        db.CircleMembers.Remove(target);
        await db.SaveChangesAsync(ct);
        return RemoveMemberOutcome.Removed;
    }

    public static int? AgeOf(DateOnly? birthDate, TimeProvider clock)
    {
        if (birthDate is not { } b) return null;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var age = today.Year - b.Year;
        if (b > today.AddYears(-age)) age--;
        return age;
    }
}
