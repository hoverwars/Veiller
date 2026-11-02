using System.ComponentModel.DataAnnotations;

namespace Veiller.Api.Health;

/// <summary>Limits of <c>POST /api/health/sync</c>: server configuration (section <c>HealthSync</c>), not constants.</summary>
public sealed class HealthSyncOptions
{
    public const string SectionName = "HealthSync";

    /// <summary>Heart rate samples + steps buckets + sleep sessions + deleted ids in one request. Records and stages do not count.</summary>
    [Range(1, 1_000_000)]
    public int MaxItemsPerRequest { get; set; } = 5000;

    /// <summary>Request body size in bytes.</summary>
    [Range(1, 64 * 1024 * 1024)]
    public long MaxBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>How far past server time a timestamp may be (phones have imperfect clocks).</summary>
    public TimeSpan FutureTolerance { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How recent <c>watchLastSeenAt</c> must be for the elder home to show the watch as connected.</summary>
    public TimeSpan WatchConnectedWindow { get; set; } = TimeSpan.FromHours(6);
}

/// <summary>Field limits that are part of the storage schema, hence of the contract.</summary>
public static class HealthLimits
{
    public const int MaxIdLength = 128;
    public const int MaxOriginLength = 255;
    public const int MinBpm = 1;
    public const int MaxBpm = 300;

    /// <summary>Health Connect accepts at most 1 000 000 steps in one record.</summary>
    public const long MaxStepsPerHour = 1_000_000;
}

public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddHealthSync(this IServiceCollection services)
    {
        services.AddOptions<HealthSyncOptions>()
            .BindConfiguration(HealthSyncOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}
