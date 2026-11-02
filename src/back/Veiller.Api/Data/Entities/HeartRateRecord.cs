namespace Veiller.Api.Data.Entities;

/// <summary>
/// One Health Connect heart rate record of an elder. Timestamps of the health tables are UTC <see cref="DateTime"/>
/// (not DateTimeOffset) so that the latest value can be aggregated in SQL on every provider.
/// </summary>
public sealed class HeartRateRecord
{
    public required string ElderId { get; init; }

    /// <summary>Health Connect <c>metadata.id</c>.</summary>
    public required string HcRecordId { get; init; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>Package name of the app that wrote the record.</summary>
    public required string Origin { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<HeartRateSample> Samples { get; init; } = [];
}
