using Veiller.Api.Auth;

namespace Veiller.Api.Data.Entities;

/// <summary>App-side profile of a Firebase user. Identity (password, claims) stays in Firebase.</summary>
public sealed class User
{
    /// <summary>Firebase uid.</summary>
    public required string Id { get; init; }

    public required string Email { get; set; }

    public required string FullName { get; set; }

    public required Role Role { get; init; }

    public string? Phone { get; set; }

    public string? Relation { get; set; }

    public DateOnly? BirthDate { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
