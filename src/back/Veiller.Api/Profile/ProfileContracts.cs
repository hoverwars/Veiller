using System.ComponentModel.DataAnnotations;
using Veiller.Api.Auth;
using Veiller.Api.Consents;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Profile;

public sealed record ProfileDto(
    string Id,
    string FullName,
    string Email,
    Role Role,
    string? Phone,
    string? Relation,
    DateOnly? BirthDate);

/// <summary>
/// Partial update: a null field is left untouched. For <c>phone</c> and <c>relation</c>, an empty string clears the value.
/// </summary>
public sealed record UpdateProfileRequest(
    [property: StringLength(100, MinimumLength = 2)] string? FullName = null,
    [property: RegularExpression(@"^(\+?[0-9 ()\-]{6,30})?$", ErrorMessage = "The phone number is not valid.")] string? Phone = null,
    [property: StringLength(50)] string? Relation = null,
    [property: BirthDate] DateOnly? BirthDate = null);

public sealed record ExportedMemberDto(string FullName, MemberRole Role);

public sealed record ExportedCircleDto(
    Guid Id,
    MemberRole MyRole,
    DateTimeOffset JoinedAt,
    string OwnerName,
    PlanTier Plan,
    IReadOnlyList<ExportedMemberDto> Members);

/// <summary>Everything the app stores about the caller (right of access). Health measurements will join it with the ingestion feature.</summary>
public sealed record ProfileExportDto(
    DateTimeOffset ExportedAt,
    DateTimeOffset CreatedAt,
    ProfileDto Profile,
    IReadOnlyList<ConsentItemDto>? Consents,
    IReadOnlyList<ExportedCircleDto> Circles);
