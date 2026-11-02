using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles.Invitations;

public sealed record InviteCodeDto(string Code, DateTimeOffset ExpiresAt);

public sealed record CirclePreviewDto(
    string Code,
    string OwnerName,
    int? OwnerAge,
    string? AdminName,
    PlanTier Plan,
    int CaregiverCount);

public sealed record JoinRequestBody(string? Code);
