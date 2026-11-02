namespace Veiller.Api.Health;

/// <summary>The valid part of a sync request, normalized to UTC and deduplicated, ready to be written.</summary>
public sealed class SyncPlan
{
    public List<PlannedHeartRateRecord> HeartRateRecords { get; } = [];

    public List<PlannedStepsHour> StepsHourly { get; } = [];

    public List<PlannedSleepSession> SleepSessions { get; } = [];

    public HashSet<string> DeletedHeartRateRecords { get; } = [];

    public HashSet<string> DeletedSleepSessions { get; } = [];

    /// <summary>Accepted liveness signal; null when absent or rejected.</summary>
    public DateTime? WatchLastSeenAt { get; set; }

    public List<RejectedItem> RejectedHeartRateRecords { get; } = [];

    public List<RejectedItem> RejectedStepsHourly { get; } = [];

    public List<RejectedItem> RejectedSleepSessions { get; } = [];

    public List<RejectedItem> RejectedDeleted { get; } = [];

    public string? RejectedWatchLastSeenAt { get; set; }

    public int AcceptedHeartRateSamples => HeartRateRecords.Sum(r => r.Samples.Count);

    public int AcceptedDeleted => DeletedHeartRateRecords.Count + DeletedSleepSessions.Count;

    public int RejectedCount =>
        RejectedHeartRateRecords.Count + RejectedStepsHourly.Count + RejectedSleepSessions.Count
        + RejectedDeleted.Count + (RejectedWatchLastSeenAt is null ? 0 : 1);
}

public sealed record PlannedSample(DateTime Time, int Bpm);

public sealed record PlannedHeartRateRecord(
    string HcRecordId, string Origin, DateTime Start, DateTime End, IReadOnlyList<PlannedSample> Samples);

public sealed record PlannedStepsHour(DateTime Start, int Count)
{
    public DateTime End => Start.AddHours(1);
}

public sealed record PlannedStage(DateTime Start, DateTime End, string Stage);

public sealed record PlannedSleepSession(
    string HcRecordId, string Origin, DateTime Start, DateTime End, string? ZoneOffset, IReadOnlyList<PlannedStage> Stages);
