namespace Veiller.Api.Data.Entities;

public sealed class SleepSession
{
    public required string ElderId { get; init; }

    public required string HcRecordId { get; init; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>UTC offset at <see cref="Start"/> (<c>±HH:MM</c>), to attribute the session to a local night. Null when the writer gave none.</summary>
    public string? ZoneOffset { get; set; }

    public required string Origin { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<SleepStage> Stages { get; init; } = [];
}
