namespace Veiller.Api.Retention;

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddRetention(this IServiceCollection services)
    {
        services.AddOptions<RetentionOptions>()
            .BindConfiguration(RetentionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHostedService<HealthRetentionWorker>();
        return services;
    }
}
