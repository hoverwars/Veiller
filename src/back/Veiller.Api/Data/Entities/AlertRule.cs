namespace Veiller.Api.Data.Entities;

/// <summary>A circle's override of the default rule of one alert type. Types without a row use the defaults.</summary>
public sealed class AlertRule
{
    public Guid CircleId { get; init; }

    public AlertType Type { get; init; }

    public bool Enabled { get; set; }

    /// <summary>bpm for heart rate rules, minutes for sleep and silence rules.</summary>
    public int Threshold { get; set; }
}
