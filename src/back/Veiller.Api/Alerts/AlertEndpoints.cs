using System.Security.Claims;
using Microsoft.Extensions.Options;
using Veiller.Api.Auth;
using Veiller.Api.Circles;
using Veiller.Api.Circles.Invitations;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/circles/{id:guid}").WithTags("Alerts").RequireAuthorization();

        group.MapGet("/alerts", ListAsync)
            .WithDescription("Alerts of a circle, newest first. Optional `status` (new, seen, resolved) and `limit` (1-200, default 50). Caregivers of the circle only; 404 otherwise.");
        group.MapPost("/alerts/{alertId:guid}/seen", MarkSeenAsync)
            .WithDescription("Marks an alert as seen. Idempotent. Caregivers of the circle only.");
        group.MapPost("/alerts/{alertId:guid}/resolve", ResolveAsync)
            .WithDescription("Resolves an alert. Idempotent. A condition that is still true does not reopen it. Caregivers of the circle only.");
        group.MapGet("/alert-rules", GetRulesAsync)
            .WithDescription("Effective alert rules of the circle (defaults and overrides). Any member.");
        group.MapPut("/alert-rules", SaveRulesAsync)
            .WithDescription("Changes alert rules (enabled, threshold). Types not in the body are unchanged. Admin or data owner only.");

        return app;
    }

    /// <summary>The caller's membership when they follow the circle as a caregiver; the data owner does not read the alerts about themselves.</summary>
    private static async Task<CircleMember?> FindCaregiverAsync(ClaimsPrincipal principal, Guid circleId, VeillerDbContext db, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return null;
        var membership = await db.FindMembershipAsync(circleId, uid, ct);
        return membership is { Role: not MemberRole.DataOwner } ? membership : null;
    }

    private static async Task<IResult> ListAsync(
        Guid id, string? status, int? limit, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (principal.Uid() is null) return Results.Unauthorized();

        var errors = new Dictionary<string, string[]>();
        AlertStatus? parsed = null;
        if (status is not null)
        {
            if (Enum.TryParse<AlertStatus>(status, ignoreCase: true, out var s) && Enum.IsDefined(s)) parsed = s;
            else errors["status"] = ["Must be one of: new, seen, resolved."];
        }
        if (limit is < 1 or > AlertService.MaxPageSize) errors["limit"] = [$"Must be between 1 and {AlertService.MaxPageSize}."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        if (await FindCaregiverAsync(principal, id, db, ct) is null) return Results.NotFound();
        return Results.Ok(await AlertService.ListAsync(db, id, parsed, limit ?? AlertService.DefaultPageSize, ct));
    }

    private static async Task<IResult> MarkSeenAsync(
        Guid id, Guid alertId, ClaimsPrincipal principal, VeillerDbContext db, CancellationToken ct)
    {
        if (principal.Uid() is null) return Results.Unauthorized();
        if (await FindCaregiverAsync(principal, id, db, ct) is null) return Results.NotFound();
        return await AlertService.MarkSeenAsync(db, id, alertId, ct) == AlertChange.Done ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ResolveAsync(
        Guid id, Guid alertId, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is null) return Results.Unauthorized();
        if (await FindCaregiverAsync(principal, id, db, ct) is null) return Results.NotFound();
        return await AlertService.ResolveAsync(db, clock, id, alertId, ct) == AlertChange.Done ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetRulesAsync(
        Guid id, ClaimsPrincipal principal, VeillerDbContext db, IOptions<AlertOptions> options, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        if (await db.FindMembershipAsync(id, uid, ct) is null) return Results.NotFound();

        var rules = await AlertService.GetRulesAsync(db, options.Value, id, ct);
        return Results.Ok(new AlertRulesDto(rules.ToDtos()));
    }

    private static async Task<IResult> SaveRulesAsync(
        Guid id, SaveAlertRulesRequest? body, ClaimsPrincipal principal, VeillerDbContext db, IOptions<AlertOptions> options, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        var actor = await db.FindMembershipAsync(id, uid, ct);
        if (actor is null) return Results.NotFound();
        if (!InvitationService.CanReview(actor))
            return Results.Problem("Only the admin or the data owner can change the alert rules.", statusCode: StatusCodes.Status403Forbidden);

        var errors = AlertRuleSet.Validate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var changes = body!.Rules!.Select(r => (r!.Type!.Value, r.Enabled!.Value, r.Threshold!.Value)).ToList();
        var current = await AlertService.GetRulesAsync(db, options.Value, id, ct);
        if (!current.With(changes).HeartRateBoundsAreConsistent)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rules"] = ["The high heart rate threshold must be above the low one."] });

        await AlertService.SaveRulesAsync(db, id, changes, ct);
        return Results.Ok(new AlertRulesDto((await AlertService.GetRulesAsync(db, options.Value, id, ct)).ToDtos()));
    }
}
