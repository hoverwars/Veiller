namespace Veiller.Api.Data.Entities;

public sealed class SleepStage
{
    public required string ElderId { get; init; }

    public required string HcRecordId { get; init; }

    /// <summary>Position in the session, which keeps the order sent by the client.</summary>
    public int Ordinal { get; init; }

    public DateTime Start { get; init; }

    public DateTime End { get; init; }

    /// <summary>One of <c>SleepStages.All</c>.</summary>
    public required string Stage { get; init; }
}
