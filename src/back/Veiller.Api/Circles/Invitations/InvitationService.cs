using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles.Invitations;

public enum JoinOutcome
{
    Created,
    NotCaregiver,
    AlreadyMember,
    AlreadyRequested,
}

public enum ReviewOutcome
{
    Done,
    NotFound,
    Forbidden,
    PlanLimitReached,
}

public static class InvitationService
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromHours(24);

    private const int MaxGenerationAttempts = 5;

    /// <summary>Replaces the circle's code by a fresh one valid for 24 hours.</summary>
    public static async Task<InviteCodeDto> GenerateCodeAsync(
        VeillerDbContext db, TimeProvider clock, Guid circleId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var now = clock.GetUtcNow();
            await db.InviteCodes.Where(c => c.CircleId == circleId).ExecuteDeleteAsync(ct);
            var invite = new InviteCode
            {
                CircleId = circleId,
                Code = InviteCodeFormat.Generate(),
                CreatedAt = now,
                ExpiresAt = now + CodeLifetime,
            };
            db.InviteCodes.Add(invite);
            try
            {
                await db.SaveChangesAsync(ct);
                return new InviteCodeDto(invite.Code, invite.ExpiresAt);
            }
            catch (DbUpdateException) when (attempt < MaxGenerationAttempts)
            {
                // Code collision with another circle (or a concurrent generation): draw another one.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Unknown and expired codes are indistinguishable: both return null.</summary>
    public static async Task<Guid?> FindCircleIdAsync(
        VeillerDbContext db, TimeProvider clock, string normalizedCode, CancellationToken ct)
    {
        // Expiry is compared in memory: the code is unique so at most one row is loaded
        // (and SQLite, used by the tests, cannot translate DateTimeOffset comparisons).
        var invite = await db.InviteCodes.AsNoTracking().SingleOrDefaultAsync(c => c.Code == normalizedCode, ct);
        return invite is not null && invite.ExpiresAt > clock.GetUtcNow() ? invite.CircleId : null;
    }

    public static async Task<CirclePreviewDto> PreviewAsync(
        VeillerDbContext db, TimeProvider clock, Guid circleId, string code, CancellationToken ct)
    {
        var circle = await db.Circles.AsNoTracking()
            .Include(c => c.Members).ThenInclude(m => m.User)
            .SingleAsync(c => c.Id == circleId, ct);
        var owner = circle.Members.Single(m => m.Role == MemberRole.DataOwner).User!;
        var admin = circle.Members.SingleOrDefault(m => m.Role == MemberRole.Admin)?.User;

        return new CirclePreviewDto(
            code, owner.FullName, CircleService.AgeOf(owner.BirthDate, clock), admin?.FullName,
            circle.Plan, circle.Members.Count(m => m.Role != MemberRole.DataOwner));
    }

    public static async Task<JoinOutcome> RequestToJoinAsync(
        VeillerDbContext db, TimeProvider clock, string userId, Guid circleId, CancellationToken ct)
    {
        var isCaregiver = await db.Users.AnyAsync(u => u.Id == userId && u.Role == Role.Caregiver, ct);
        if (!isCaregiver) return JoinOutcome.NotCaregiver;
        if (await db.CircleMembers.AnyAsync(m => m.CircleId == circleId && m.UserId == userId, ct))
            return JoinOutcome.AlreadyMember;
        if (await db.JoinRequests.AnyAsync(r => r.CircleId == circleId && r.UserId == userId, ct))
            return JoinOutcome.AlreadyRequested;

        db.JoinRequests.Add(new JoinRequest { CircleId = circleId, UserId = userId, CreatedAt = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent identical request won the unique index.
            db.ChangeTracker.Clear();
            return JoinOutcome.AlreadyRequested;
        }
        return JoinOutcome.Created;
    }

    public static async Task<List<JoinRequestDto>> PendingRequestsAsync(
        VeillerDbContext db, Guid circleId, CancellationToken ct)
    {
        var requests = await db.JoinRequests.AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.CircleId == circleId)
            .ToListAsync(ct);
        // Ordered in memory: SQLite (tests) cannot order by DateTimeOffset.
        return requests.OrderBy(r => r.CreatedAt).Select(r => new JoinRequestDto(r.Id.ToString(), r.User!.FullName)).ToList();
    }

    /// <summary>
    /// Approves a request: the caregiver joins the circle. The first caregiver of a circle becomes its admin and sponsor.
    /// </summary>
    public static async Task<ReviewOutcome> ApproveAsync(
        VeillerDbContext db, TimeProvider clock, CircleMember actor, Guid requestId, CancellationToken ct)
    {
        if (!CanReview(actor)) return ReviewOutcome.Forbidden;
        var request = await FindRequestAsync(db, actor.CircleId, requestId, ct);
        if (request is null) return ReviewOutcome.NotFound;

        var circle = await db.Circles.AsNoTracking().SingleAsync(c => c.Id == actor.CircleId, ct);
        var roles = await db.CircleMembers.AsNoTracking()
            .Where(m => m.CircleId == actor.CircleId).Select(m => m.Role).ToListAsync(ct);
        if (roles.Count(r => r != MemberRole.DataOwner) >= PlanLimits.MaxCaregivers(circle.Plan))
            return ReviewOutcome.PlanLimitReached;

        var becomesAdmin = !roles.Contains(MemberRole.Admin);
        try
        {
            await AddMemberAsync(db, clock, request, becomesAdmin, ct);
        }
        catch (DbUpdateException) when (becomesAdmin)
        {
            // Another approval took the admin seat first (unique index): retry as a plain caregiver.
            db.ChangeTracker.Clear();
            request = await FindRequestAsync(db, actor.CircleId, requestId, ct);
            if (request is null) return ReviewOutcome.NotFound;
            await AddMemberAsync(db, clock, request, asAdmin: false, ct);
        }
        return ReviewOutcome.Done;
    }

    public static async Task<ReviewOutcome> RejectAsync(
        VeillerDbContext db, CircleMember actor, Guid requestId, CancellationToken ct)
    {
        if (!CanReview(actor)) return ReviewOutcome.Forbidden;
        var request = await FindRequestAsync(db, actor.CircleId, requestId, ct);
        if (request is null) return ReviewOutcome.NotFound;

        db.JoinRequests.Remove(request);
        await db.SaveChangesAsync(ct);
        return ReviewOutcome.Done;
    }

    public static bool CanReview(CircleMember member) => member.Role is MemberRole.Admin or MemberRole.DataOwner;

    private static async Task AddMemberAsync(
        VeillerDbContext db, TimeProvider clock, JoinRequest request, bool asAdmin, CancellationToken ct)
    {
        db.JoinRequests.Remove(request);
        db.CircleMembers.Add(new CircleMember
        {
            CircleId = request.CircleId,
            UserId = request.UserId,
            Role = asAdmin ? MemberRole.Admin : MemberRole.Caregiver,
            IsSponsor = asAdmin,
            JoinedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    private static Task<JoinRequest?> FindRequestAsync(
        VeillerDbContext db, Guid circleId, Guid requestId, CancellationToken ct) =>
        db.JoinRequests.SingleOrDefaultAsync(r => r.CircleId == circleId && r.Id == requestId, ct);
}
