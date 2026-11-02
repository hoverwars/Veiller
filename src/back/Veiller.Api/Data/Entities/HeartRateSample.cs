namespace Veiller.Api.Data.Entities;

public sealed class HeartRateSample
{
    public required string ElderId { get; init; }

    public required string HcRecordId { get; init; }

    public DateTime Time { get; init; }

    public int Bpm { get; init; }
}
