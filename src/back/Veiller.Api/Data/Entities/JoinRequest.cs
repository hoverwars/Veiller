namespace Veiller.Api.Data.Entities;

/// <summary>A caregiver's pending request to enter a circle. Deleted once approved or rejected.</summary>
public sealed class JoinRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CircleId { get; init; }

    public required string UserId { get; init; }

    public User? User { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
