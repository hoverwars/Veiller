using System.Security.Claims;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Patients;

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthorization();

        group.MapGet("/", ListAsync)
            .WithDescription("Summary of every elder the current user follows as a caregiver. Health fields are null when unknown.");
        group.MapGet("/{circleId:guid}", GetAsync)
            .WithDescription("Summary of one patient. 404 when the user is not a caregiver of that circle.");
        group.MapGet("/{circleId:guid}/history", GetHistoryAsync)
            .WithDescription("Resting heart rate and sleep hours of one patient. `range` = day|week|month; `tzOffset` (+02:00, default UTC) sets the local day.");

        return app;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();

        var summaries = new List<PatientSummaryDto>();
        foreach (var circle in await PatientSummaryService.ListFollowedCirclesAsync(db, uid, ct))
            summaries.Add(await PatientSummaryService.BuildAsync(db, clock, circle, ct));
        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetAsync(
        Guid circleId, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        if (await PatientSummaryService.FindFollowedCircleAsync(db, circleId, uid, ct) is not { } circle) return Results.NotFound();

        return Results.Ok(await PatientSummaryService.BuildAsync(db, clock, circle, ct));
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid circleId, string? range, string? tzOffset, ClaimsPrincipal principal, VeillerDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (principal.Uid() is not { } uid) return Results.Unauthorized();
        // Access first: a stranger gets 404 whatever the query, so nothing is revealed by the validation errors.
        if (await PatientSummaryService.FindFollowedCircleAsync(db, circleId, uid, ct) is not { } circle) return Results.NotFound();

        var (parsedRange, offset, error) = PatientQueryValidator.ParseHistory(range, tzOffset);
        if (error is not null) return error;

        return Results.Ok(await PatientHistoryService.BuildAsync(db, clock, PatientSummaryService.OwnerOf(circle).Id, parsedRange, offset, ct));
    }
}
