using Veiller.Api.Data.Entities;

namespace Veiller.Api.Patients;

/// <summary>
/// What a caregiver sees of one elder. Every health field is nullable: no data, an expired reading or a missing consent
/// all give <c>null</c>. SpO2, blood pressure and battery are not ingested yet (Health Connect v1) and are always null.
/// </summary>
public sealed record PatientSummaryDto(
    Guid CircleId,
    string FullName,
    int? Age,
    PlanTier Plan,
    int CaregiverCount,
    int? HeartRateBpm,
    int? SleepMinutes,
    int? Spo2Percent,
    string? BloodPressure,
    int? BatteryPercent,
    int LastSyncMinutesAgo,
    DateTimeOffset? LastSyncAt);

public enum HistoryRange
{
    Day,
    Week,
    Month,
}

/// <summary><c>Label</c> is ready to display; <c>Timestamp</c> (start of the bucket, UTC) lets a client build its own.</summary>
public sealed record DataPointDto(string Label, float? Value, DateTimeOffset Timestamp);

public sealed record PatientHistoryDto(
    HistoryRange Range,
    IReadOnlyList<DataPointDto> RestingHeartRate,
    IReadOnlyList<DataPointDto> SleepHours);
