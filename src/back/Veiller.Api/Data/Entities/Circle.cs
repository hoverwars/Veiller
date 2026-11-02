namespace Veiller.Api.Data.Entities;

public enum PlanTier
{
    Free,
    Premium,
}

public enum MemberRole
{
    DataOwner,
    Admin,
    Caregiver,
}

/// <summary>Links one elder (the data owner) to their caregivers.</summary>
public sealed class Circle
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string OwnerUserId { get; init; }

    public PlanTier Plan { get; set; } = PlanTier.Free;

    public DateTimeOffset CreatedAt { get; init; }

    public List<CircleMember> Members { get; init; } = [];
}
