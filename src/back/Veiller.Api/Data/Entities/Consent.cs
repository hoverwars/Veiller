using Veiller.Api.Consents;

namespace Veiller.Api.Data.Entities;

/// <summary>A data owner's decision about one kind of health data. One row per (user, type).</summary>
public sealed class Consent
{
    public required string UserId { get; init; }

    public HealthDataType Type { get; init; }

    public bool Granted { get; set; }

    /// <summary>When the data was last granted; null if never granted.</summary>
    public DateTimeOffset? GrantedAt { get; set; }

    /// <summary>When the consent was last withdrawn; null while granted or never withdrawn.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
