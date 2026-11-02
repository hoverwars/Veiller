using System.Security.Claims;
using Veiller.Api.Auth;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles.Invitations;

public static class InvitationEndpoints
{
    public const string RateLimitPolicy = "invitations";

    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/circles").WithTags("Invitations").RequireAuthorization();

        group.MapPost("/{id:guid}/invite-code", GenerateCodeAsync)
            .WithDescription("Generates a new invitation code (valid 24 h, reusable) and invalidates the previous one. Admin or data owner only.");
        group.MapGet("/by-code/{code}", PreviewAsync).RequireRateLimiting(RateLimitPolicy)
            .WithDescription("Preview of the circle behind a code. 404 for unknown and expired codes alike. Rate limited.");
        group.MapPost("/join-requests", RequestToJoinAsync).RequireRateLimiting(RateLimitPolicy)
            .WithDescription("A caregiver asks to join the circle behind a code. Rate limited.");
        group.MapPost("/{id:guid}/requests/{requestId:guid}/approve", ApproveAsync)
            .WithDescription("Approves a join request. 409 when the plan's caregiver limit is reached. Admin or data owner only.");
        group.MapPost("/{id:guid}/requests/{requestId:guid}/reject", RejectAsync)
            .WithDescription("Rejects a join request. Admin or data owner only.");

        return app;
    }

    private static async Task<IResult> GenerateCodeAsync(
        Guid id, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        var actor = await db.FindMembershipAsync(id, uid, ct);
        if (actor is null) return Results.NotFound();
        if (!InvitationService.CanReview(actor)) return Forbidden("Only the admin or the data owner can invite people.");

        return Results.Ok(await InvitationService.GenerateCodeAsync(db, clock, id, ct));
    }

    private static async Task<IResult> PreviewAsync(
        string code, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is null) return Results.Unauthorized();
        if (InviteCodeFormat.Normalize(code) is not { } normalized) return InvalidCode();
        if (await InvitationService.FindCircleIdAsync(db, clock, normalized, ct) is not { } circleId)
            return CodeNotFound();

        return Results.Ok(await InvitationService.PreviewAsync(db, clock, circleId, normalized, ct));
    }

    private static async Task<IResult> RequestToJoinAsync(
        JoinRequestBody? body, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        if (InviteCodeFormat.Normalize(body?.Code) is not { } normalized) return InvalidCode();
        if (await InvitationService.FindCircleIdAsync(db, clock, normalized, ct) is not { } circleId)
            return CodeNotFound();

        return await InvitationService.RequestToJoinAsync(db, clock, uid, circleId, ct) switch
        {
            JoinOutcome.Created => Results.Accepted(),
            JoinOutcome.NotCaregiver => Forbidden("Only caregivers can join a circle."),
            JoinOutcome.AlreadyMember => Conflict("You are already a member of this circle."),
            _ => Conflict("You already have a pending request for this circle."),
        };
    }

    private static Task<IResult> ApproveAsync(
        Guid id, Guid requestId, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct) =>
        ReviewAsync(id, principal, db, actor => InvitationService.ApproveAsync(db, clock, actor, requestId, ct), ct);

    private static Task<IResult> RejectAsync(
        Guid id, Guid requestId, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct) =>
        ReviewAsync(id, principal, db, actor => InvitationService.RejectAsync(db, actor, requestId, ct), ct);

    private static async Task<IResult> ReviewAsync(
        Guid id, ClaimsPrincipal principal, VeillerDbContext db,
        Func<CircleMember, Task<ReviewOutcome>> review, CancellationToken ct)
    {
        var uid = principal.Uid();
        if (uid is null) return Results.Unauthorized();
        var actor = await db.FindMembershipAsync(id, uid, ct);
        if (actor is null) return Results.NotFound();

        return await review(actor) switch
        {
            ReviewOutcome.Done => Results.NoContent(),
            ReviewOutcome.NotFound => Results.NotFound(),
            ReviewOutcome.PlanLimitReached => Conflict("Limite du plan atteinte"),
            _ => Forbidden("Only the admin or the data owner can review join requests."),
        };
    }

    private static IResult Forbidden(string detail) => Results.Problem(detail, statusCode: StatusCodes.Status403Forbidden);

    private static IResult Conflict(string detail) => Results.Problem(detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult CodeNotFound() =>
        Results.Problem("No circle matches this code.", statusCode: StatusCodes.Status404NotFound);

    private static IResult InvalidCode() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["The code must look like VLR-XXXX."] });
}
