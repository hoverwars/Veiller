using Microsoft.EntityFrameworkCore;
using Veiller.Api.Circles;
using Veiller.Api.Circles.Invitations;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Billing;

public enum SubscribeOutcome
{
    Subscribed,
    InvalidPurchase,
    PurchaseAlreadyUsed,
}

public static class PlanService
{
    /// <summary>The sponsor of the circle: the only member who pays for it, hence the only one who changes its plan.</summary>
    public static bool IsSponsor(CircleMember member) => member is { IsSponsor: true, Role: not MemberRole.DataOwner };

    /// <summary>
    /// Moves the circle to Premium once the store confirms the purchase. Repeating the call with the same token is harmless;
    /// a token already paying for another circle is refused.
    /// </summary>
    public static async Task<SubscribeOutcome> SubscribeAsync(
        VeillerDbContext db, IPurchaseVerifier verifier, Guid circleId, string purchaseToken, CancellationToken ct)
    {
        var usedElsewhere = await db.Circles.AnyAsync(c => c.PlanPurchaseToken == purchaseToken && c.Id != circleId, ct);
        if (usedElsewhere) return SubscribeOutcome.PurchaseAlreadyUsed;
        if (!await verifier.VerifyAsync(purchaseToken, ct)) return SubscribeOutcome.InvalidPurchase;

        var circle = await db.Circles.SingleAsync(c => c.Id == circleId, ct);
        circle.Plan = PlanTier.Premium;
        circle.PlanPurchaseToken = purchaseToken;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same token was attached to another circle between the check and the save.
            return SubscribeOutcome.PurchaseAlreadyUsed;
        }
        return SubscribeOutcome.Subscribed;
    }

    /// <summary>
    /// Moves the circle back to Free. Caregivers above the Free limit leave the circle: the admin and the sponsor stay,
    /// then the earliest to have joined. Returns the members removed, in the order they were removed.
    /// </summary>
    public static async Task<List<CircleMember>> DowngradeAsync(VeillerDbContext db, Guid circleId, CancellationToken ct)
    {
        var circle = await db.Circles.Include(c => c.Members).ThenInclude(m => m.User).SingleAsync(c => c.Id == circleId, ct);
        circle.Plan = PlanTier.Free;
        circle.PlanPurchaseToken = null;

        var caregivers = circle.Members.Where(m => m.Role != MemberRole.DataOwner).ToList();
        var surplus = caregivers.Count - PlanLimits.MaxCaregivers(PlanTier.Free);
        var removed = surplus <= 0
            ? []
            : caregivers
                .OrderBy(m => m.Role == MemberRole.Admin || m.IsSponsor ? 0 : 1)
                .ThenBy(m => m.JoinedAt)
                .ThenBy(m => m.UserId, StringComparer.Ordinal)
                .TakeLast(surplus)
                .Reverse()
                .ToList();

        db.CircleMembers.RemoveRange(removed);
        await db.SaveChangesAsync(ct);
        return removed;
    }
}
