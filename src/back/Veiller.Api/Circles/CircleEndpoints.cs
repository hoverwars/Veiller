using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Circles.Invitations;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles;

public static class CircleEndpoints
{
    public static IEndpointRouteBuilder MapCircleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/circles").WithTags("Circles").RequireAuthorization();

        group.MapGet("/", ListAsync)
            .WithDescription("Circles where the current user is a caregiver (admin or caregiver).");
        group.MapGet("/own", GetOwnAsync)
            .WithDescription("The circle owned by the current elder. 404 until the onboarding is completed.");
        group.MapGet("/{id:guid}", GetAsync)
            .WithDescription("Circle detail. 404 when the user is not a member, so the circle's existence is not revealed.");
        group.MapDelete("/{id:guid}/members/{userId}", RemoveMemberAsync)
            .WithDescription("Removes a member. The owner can never be removed; a caregiver can only remove themselves.");

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        var ids = await db.CircleMembers.AsNoTracking()
            .Where(m => m.UserId == uid && m.Role != MemberRole.DataOwner)
            .Select(m => m.CircleId)
            .ToListAsync(ct);

        var circles = await db.Circles.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Include(c => c.Members).ThenInclude(m => m.User)
            .ToListAsync(ct);

        return Results.Ok(circles.Select(c =>
        {
            var owner = c.Members.Single(m => m.Role == MemberRole.DataOwner).User!;
            return new CircleSummaryDto(
                c.Id, owner.FullName, CircleService.AgeOf(owner.BirthDate, clock), c.Plan,
                c.Members.Count(m => m.Role != MemberRole.DataOwner));
        }));
    }

    private static async Task<IResult> GetOwnAsync(
        ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();

        var circleId = await db.Circles.Where(c => c.OwnerUserId == uid)
            .Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);
        return circleId is null ? Results.NotFound() : await LoadDetailAsync(db, circleId.Value, clock, ct);
    }

    private static async Task<IResult> GetAsync(
        Guid id, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        if (await db.FindMembershipAsync(id, uid, ct) is null) return Results.NotFound();

        return await LoadDetailAsync(db, id, clock, ct);
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid id, string userId, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        var actor = await db.FindMembershipAsync(id, uid, ct);
        if (actor is null) return Results.NotFound();

        return await CircleService.RemoveMemberAsync(db, actor, userId, ct) switch
        {
            RemoveMemberOutcome.Removed => Results.NoContent(),
            RemoveMemberOutcome.NotFound => Results.NotFound(),
            RemoveMemberOutcome.CannotRemoveOwner => Results.Problem(
                "The data owner cannot be removed from their circle.", statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem("You are not allowed to remove this member.", statusCode: StatusCodes.Status403Forbidden),
        };
    }

    private static async Task<IResult> LoadDetailAsync(VeillerDbContext db, Guid id, TimeProvider clock, CancellationToken ct)
    {
        var circle = await db.Circles.AsNoTracking()
            .Include(c => c.Members).ThenInclude(m => m.User)
            .SingleAsync(c => c.Id == id, ct);

        var members = circle.Members.OrderBy(m => m.JoinedAt).Select(ToDto).ToList();
        var owner = members.Single(m => m.Role == MemberRole.DataOwner);
        var ownerUser = circle.Members.Single(m => m.Role == MemberRole.DataOwner).User!;

        return Results.Ok(new CircleDto(
            circle.Id, owner, CircleService.AgeOf(ownerUser.BirthDate, clock), circle.Plan, members, await InvitationService.PendingRequestsAsync(db, id, ct)));
    }

    private static CircleMemberDto ToDto(CircleMember m) =>
        new(m.UserId, m.User!.FullName, m.Role, m.User.Relation, m.IsSponsor, m.User.Phone);
}
