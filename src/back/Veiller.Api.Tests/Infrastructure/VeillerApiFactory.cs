using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Veiller.Api.Auth;

namespace Veiller.Api.Tests.Infrastructure;

/// <summary>Runs the real API in memory with both Firebase entry points replaced by fakes (no network, no credentials).</summary>
public sealed class VeillerApiFactory : WebApplicationFactory<Program>
{
    public FakeFirebaseIdentityClient Identity { get; } = new();

    public FakeFirebaseAdmin Admin { get; } = new();

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
            services.AddSingleton<IFirebaseAdmin>(Admin);
            services.AddSingleton<IFirebaseIdentityClient>(Identity);
        });
    }
}
