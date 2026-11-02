namespace Veiller.Api.Health;

public static class SleepStages
{
    public const string Unknown = "UNKNOWN";

    /// <summary>Wire names, mapped by the client from <c>SleepSessionRecord.STAGE_TYPE_*</c>.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "AWAKE", "SLEEPING", "OUT_OF_BED", "LIGHT", "DEEP", "REM", "AWAKE_IN_BED", Unknown,
    };

    /// <summary>A known stage keeps its name (case-insensitive); anything else, including a null, is stored as UNKNOWN.</summary>
    public static string Normalize(string? stage)
    {
        var candidate = stage?.Trim().ToUpperInvariant();
        return candidate is not null && All.Contains(candidate) ? candidate : Unknown;
    }
}
