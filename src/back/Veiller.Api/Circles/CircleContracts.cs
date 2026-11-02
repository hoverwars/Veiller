using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles;

public sealed record CircleMemberDto(
    string UserId,
    string FullName,
    MemberRole Role,
    string? Relation,
    bool IsSponsor,
    string? Phone);

public sealed record JoinRequestDto(string Id, string FullName);

public sealed record CircleDto(
    Guid Id,
    CircleMemberDto Owner,
    int? OwnerAge,
    PlanTier Plan,
    IReadOnlyList<CircleMemberDto> Members,
    IReadOnlyList<JoinRequestDto> PendingRequests);

public sealed record CircleSummaryDto(
    Guid Id,
    string OwnerName,
    int? OwnerAge,
    PlanTier Plan,
    int CaregiverCount);
