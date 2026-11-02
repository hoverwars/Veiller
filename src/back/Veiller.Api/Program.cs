using System.Text.Json.Serialization;
using Veiller.Api.Auth;
using System.Threading.RateLimiting;
using Veiller.Api.Circles;
using Veiller.Api.Circles.Invitations;
using Veiller.Api.Consents;
using Veiller.Api.Data;
using Veiller.Api.Health;
using Veiller.Api.Profile;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Veiller API", Version = "v1" });
});
builder.Services.AddBearerSwagger();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddVeillerData(builder.Configuration);
builder.Services.AddFirebaseAuth(builder.Configuration);
builder.Services.AddHealthSync();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Slows down brute force on invitation codes: per authenticated user (per IP when anonymous).
    var perMinute = builder.Configuration.GetValue("RateLimiting:InvitationsPerMinute", 10);
    options.AddPolicy(InvitationEndpoints.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Uid() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Veiller API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithDescription("Basic endpoint to check that the API is running.");

app.MapAuthEndpoints();
app.MapCircleEndpoints();
app.MapInvitationEndpoints();
app.MapConsentEndpoints();
app.MapProfileEndpoints();
app.MapHealthSyncEndpoints();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program;
