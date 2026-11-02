using System.ComponentModel.DataAnnotations;

namespace Veiller.Api.Retention;

/// <summary>How long raw health data is kept (section <c>Retention</c>). Older data is deleted by a daily background job.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Set to false to disable the background job (tests, or a deployment that purges elsewhere).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Raw heart rate, steps and sleep older than this many days are deleted.</summary>
    [Range(30, 3650)]
    public int HealthDataDays { get; set; } = 365;

    /// <summary>Time between two purges.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);
}
