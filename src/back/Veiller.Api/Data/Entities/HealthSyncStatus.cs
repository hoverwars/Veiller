namespace Veiller.Api.Data.Entities;

/// <summary>Sync bookkeeping of one elder, written by every successful <c>POST /api/health/sync</c>.</summary>
public sealed class HealthSyncStatus
{
    public required string ElderId { get; init; }

    public DateTime LastUploadAt { get; set; }

    /// <summary>Watch liveness signal: the maximum ever received.</summary>
    public DateTime? WatchLastSeenAt { get; set; }
}
