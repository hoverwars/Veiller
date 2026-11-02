using System.ComponentModel.DataAnnotations;

namespace Veiller.Api.Alerts;

/// <summary>Defaults of the alert rules and cadence of the background evaluation (section <c>Alerts</c>).</summary>
public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>Set to false to disable the periodic evaluation (tests). Alerts are still evaluated at each sync.</summary>
    public bool WorkerEnabled { get; set; } = true;

    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Only a heart rate sample at most this old is judged: an old reading says nothing about now.</summary>
    public TimeSpan HeartRateFreshness { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Default heart rate ceiling in bpm. Product defaults, to validate with a medical advisor.</summary>
    [Range(1, 300)]
    public int HeartRateHigh { get; set; } = 120;

    [Range(1, 300)]
    public int HeartRateLow { get; set; } = 40;

    /// <summary>Default minimum of sleep in the last night, in minutes.</summary>
    [Range(1, 1440)]
    public int SleepLowMinutes { get; set; } = 240;

    /// <summary>Default silence of the watch, in minutes, before an alert.</summary>
    [Range(1, 10080)]
    public int NoSyncMinutes { get; set; } = 360;
}

public static class AlertServiceCollectionExtensions
{
    public static IServiceCollection AddAlerts(this IServiceCollection services)
    {
        services.AddOptions<AlertOptions>()
            .BindConfiguration(AlertOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<AlertNotifier>();
        services.AddSingleton<IPushSender, FcmPushSender>();
        services.AddHostedService<AlertWorker>();
        return services;
    }
}
