using System.Security.Claims;
using Veiller.Api.Auth;
using Veiller.Api.Circles;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Billing;

public sealed record SubscribeRequest(string? PurchaseToken);

/// <summary><c>RemovedMembers</c> lists the caregivers who left the circle because of a downgrade (empty otherwise).</summary>
public sealed record PlanChangeDto(Guid CircleId, PlanTier Plan, IReadOnlyList<CircleMemberDto> RemovedMembers);

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/circles/{id:guid}/plan").WithTags("Plan").RequireAuthorization();

        group.MapPost("/subscribe", SubscribeAsync)
            .WithDescription("Moves the circle to Premium after the store confirms `purchaseToken`. Sponsor only. 402 when the purchase is not valid, 409 when it already pays for another circle, 501 while no store is connected.");
        group.MapPost("/cancel", CancelAsync)
            .WithDescription("Moves the circle back to Free. Sponsor only. Caregivers above the Free limit are removed (admin and sponsor stay, then the earliest to have joined) and returned in `removedMembers`.");
        return app;
    }

    /// <summary>The caller's membership when they are the sponsor; otherwise the error to return (404 for non-members, 403 for the others).</summary>
    private static async Task<(CircleMember? Sponsor, IResult? Error)> RequireSponsorAsync(
        Guid circleId, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return (null, Results.Unauthorized());
        var member = await db.FindMembershipAsync(circleId, uid, ct);
        if (member is null) return (null, Results.NotFound());
        return PlanService.IsSponsor(member)
            ? (member, null)
            : (null, Results.Problem("Only the sponsor of the circle can change its plan.", statusCode: StatusCodes.Status403Forbidden));
    }

    private static async Task<IResult> SubscribeAsync(
        Guid id, SubscribeRequest? body, ClaimsPrincipal principal, VeillerDbContext db, IPurchaseVerifier verifier, CancellationToken ct)
    {
        var (sponsor, error) = await RequireSponsorAsync(id, principal, db, ct);
        if (sponsor is null) return error!;

        var token = body?.PurchaseToken?.Trim();
        if (string.IsNullOrEmpty(token))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["purchaseToken"] = ["Required."] });
        if (token.Length > Data.Configurations.PurchaseTokenLimits.MaxLength)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["purchaseToken"] = ["Too long."] });

        try
        {
            return await PlanService.SubscribeAsync(db, verifier, id, token, ct) switch
            {
                SubscribeOutcome.Subscribed => Results.Ok(new PlanChangeDto(id, PlanTier.Premium, [])),
                SubscribeOutcome.PurchaseAlreadyUsed => Results.Problem("This purchase already pays for another circle.", statusCode: StatusCodes.Status409Conflict),
                _ => Results.Problem("The purchase could not be verified.", statusCode: StatusCodes.Status402PaymentRequired),
            };
        }
        catch (NotImplementedException)
        {
            return Results.Problem("Subscriptions are not available yet.", statusCode: StatusCodes.Status501NotImplemented);
        }
    }

    private static async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var (sponsor, error) = await RequireSponsorAsync(id, principal, db, ct);
        if (sponsor is null) return error!;

        var removed = await PlanService.DowngradeAsync(db, id, ct);
        return Results.Ok(new PlanChangeDto(id, PlanTier.Free, removed.Select(CircleEndpoints.ToDto).ToList()));
    }
}
