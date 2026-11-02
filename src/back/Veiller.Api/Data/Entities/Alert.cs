namespace Veiller.Api.Data.Entities;

public enum AlertType
{
    HeartRateHigh,
    HeartRateLow,
    SleepLow,
    NoSync,
}

public enum AlertStatus
{
    New,
    Seen,
    Resolved,
}

/// <summary>
/// One episode of an alert condition in a circle. A condition that stays true does not open a second alert:
/// <see cref="Active"/> stays set until the evaluator observes the condition is over, even if a caregiver resolved the alert earlier.
/// </summary>
public sealed class Alert
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CircleId { get; init; }

    public AlertType Type { get; init; }

    public AlertStatus Status { get; set; }

    /// <summary>True while the condition that opened the alert has not been seen to end.</summary>
    public bool Active { get; set; } = true;

    public DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The measure that crossed the threshold: bpm for heart rate, minutes for sleep and for the silence of the watch.</summary>
    public int? Value { get; init; }
}
