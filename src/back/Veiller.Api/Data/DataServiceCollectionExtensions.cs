using Microsoft.EntityFrameworkCore;

namespace Veiller.Api.Data;

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "Veiller";

    public static IServiceCollection AddVeillerData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<VeillerDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));
        return services;
    }

    /// <summary>Applies pending EF Core migrations. Called once at startup.</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VeillerDbContext>();
        // Migrations are generated for PostgreSQL; other providers (SQLite in tests) create their schema themselves.
        if (db.Database.IsNpgsql())
            await db.Database.MigrateAsync();
    }
}
