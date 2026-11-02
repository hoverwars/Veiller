namespace Veiller.Api.Data.Entities;

/// <summary>The current invitation code of a circle. A circle has at most one; generating a new code replaces it.</summary>
public sealed class InviteCode
{
    public Guid CircleId { get; init; }

    /// <summary>Normalized form, e.g. <c>VLR-7K2P</c>.</summary>
    public required string Code { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
