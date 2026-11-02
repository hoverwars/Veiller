namespace Veiller.Api.Data.Entities;

public sealed class CircleMember
{
    public Guid CircleId { get; init; }

    public required string UserId { get; init; }

    public User? User { get; init; }

    public MemberRole Role { get; set; }

    public bool IsSponsor { get; set; }

    public DateTimeOffset JoinedAt { get; init; }
}
