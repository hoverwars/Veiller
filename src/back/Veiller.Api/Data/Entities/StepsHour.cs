namespace Veiller.Api.Data.Entities;

/// <summary>Steps of one whole UTC hour, already deduplicated across apps by Health Connect.</summary>
public sealed class StepsHour
{
    public required string ElderId { get; init; }

    public DateTime Start { get; init; }

    public DateTime End { get; set; }

    public int Count { get; set; }

    public DateTime UpdatedAt { get; set; }
}
