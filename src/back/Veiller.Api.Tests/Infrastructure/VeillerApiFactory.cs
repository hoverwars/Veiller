using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Veiller.Api.Auth;
using Veiller.Api.Data;

namespace Veiller.Api.Tests.Infrastructure;

/// <summary>Runs the real API in memory with both Firebase entry points replaced by fakes (no network, no credentials).</summary>
public sealed class VeillerApiFactory : WebApplicationFactory<Program>
{
    public FakeFirebaseIdentityClient Identity { get; } = new();

    public FakeFirebaseAdmin Admin { get; } = new();

    // An in-memory SQLite database lives as long as its connection: keep one open per factory (isolated per test class).
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Opens a scope-bound context on the test database, to seed or assert on stored data.</summary>
    public IServiceScope CreateDbScope(out VeillerDbContext db)
    {
        var scope = Services.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<VeillerDbContext>();
        return scope;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Required by FirebaseOptions (ValidateOnStart); empty in appsettings.json.
        builder.UseSetting("Firebase:ProjectId", "test-project");
        builder.UseSetting("Firebase:WebApiKey", "test-api-key");

        builder.ConfigureServices(services =>
        {
            // The FirebaseApp / FirebaseAuth singletons are lazy and only resolved by the real facade,
            // so replacing these two services means they are never built.
            services.RemoveAll<IFirebaseAdmin>();
            services.RemoveAll<IFirebaseIdentityClient>();
            services.RemoveAll<DbContextOptions<VeillerDbContext>>();
            // EF registers the Npgsql setup as an internal IDbContextOptionsConfiguration<VeillerDbContext>: drop it too.
            foreach (var d in services.Where(d => d.ServiceType.IsGenericType
                         && d.ServiceType.GenericTypeArguments.SequenceEqual([typeof(VeillerDbContext)])
                         && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")).ToList())
                services.Remove(d);
            services.AddDbContext<VeillerDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<IFirebaseAdmin>(Admin);
            services.AddSingleton<IFirebaseIdentityClient>(Identity);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<VeillerDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
