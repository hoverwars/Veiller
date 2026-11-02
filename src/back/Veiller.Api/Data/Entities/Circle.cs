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

    /// <summary>The store purchase that pays for the Premium plan; null on the Free plan. One purchase pays for one circle.</summary>
    public string? PlanPurchaseToken { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<CircleMember> Members { get; init; } = [];
}
