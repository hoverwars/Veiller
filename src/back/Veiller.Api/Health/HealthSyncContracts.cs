using System.Text.Json.Serialization;

namespace Veiller.Api.Health;

// Request members are nullable on purpose: a missing field must become a per-item rejection (MISSING_FIELD),
// not a silent default and not a failure of the whole request. Wrong JSON types still fail the request (400).

public sealed record SyncRequest(
    int? SchemaVersion,
    SyncClientInfo? Client,
    DateTimeOffset? WatchLastSeenAt,
    List<HeartRateRecordItem?>? HeartRateRecords,
    List<StepsBucketItem?>? StepsHourly,
    List<SleepSessionItem?>? SleepSessions,
    DeletedItems? Deleted);

/// <summary>Informational only: never stored, never logged beyond counts.</summary>
public sealed record SyncClientInfo(string? AppVersion, string? DeviceManufacturer, string? DeviceModel);

public sealed record HeartRateRecordItem(
    string? HcRecordId,
    string? Origin,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    List<HeartRateSampleItem?>? Samples);

/// <summary><c>Bpm</c> is a long so that an absurd value is a BPM_OUT_OF_RANGE rejection, not a 400 for the whole request.</summary>
public sealed record HeartRateSampleItem(DateTimeOffset? Time, long? Bpm);

/// <summary><c>Count</c> is a long for the same reason as <see cref="HeartRateSampleItem.Bpm"/>.</summary>
public sealed record StepsBucketItem(DateTimeOffset? Start, DateTimeOffset? End, long? Count);

public sealed record SleepSessionItem(
    string? HcRecordId,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string? ZoneOffset,
    string? Origin,
    List<SleepStageItem?>? Stages);

public sealed record SleepStageItem(DateTimeOffset? Start, DateTimeOffset? End, string? Stage);

public sealed record DeletedItems(List<string?>? HeartRateRecords, List<string?>? SleepSessions);

public sealed record SyncResponse(SyncAccepted Accepted, SyncRejected Rejected, DateTimeOffset ServerTime);

public sealed record SyncAccepted(int HeartRateSamples, int StepsHourly, int SleepSessions, int Deleted);

public sealed record SyncRejected(
    IReadOnlyList<RejectedItem> HeartRateRecords,
    IReadOnlyList<RejectedItem> StepsHourly,
    IReadOnlyList<RejectedItem> SleepSessions,
    IReadOnlyList<RejectedItem> Deleted,
    string? WatchLastSeenAt);

/// <summary>
/// Position and reason only, never the rejected value (rejections can end up in logs).
/// <c>SampleIndex</c> is set when only one heart rate sample was dropped; <c>Block</c> only for deleted ids.
/// </summary>
public sealed record RejectedItem(
    int Index,
    string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SampleIndex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Block = null);

public static class RejectionReason
{
    public const string BpmOutOfRange = "BPM_OUT_OF_RANGE";
    public const string TimeInFuture = "TIME_IN_FUTURE";
    public const string InvalidInterval = "INVALID_INTERVAL";
    public const string OutsideParent = "OUTSIDE_PARENT";
    public const string BucketNotAligned = "BUCKET_NOT_ALIGNED";
    public const string MissingField = "MISSING_FIELD";
    public const string FieldTooLong = "FIELD_TOO_LONG";

    // Not in the first version of the spec (see docs/dev/health_sync_backend_notes.md).
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string CountOutOfRange = "COUNT_OUT_OF_RANGE";
    public const string TooManySamples = "TOO_MANY_SAMPLES";
    public const string TimeTooOld = "TIME_TOO_OLD";
}

public sealed record HealthStatusDto(DateTimeOffset? LastUploadAt, DateTimeOffset? WatchLastSeenAt, LastSampleDto LastSample);

public sealed record LastSampleDto(DateTimeOffset? HeartRate, DateTimeOffset? Steps, DateTimeOffset? Sleep);
