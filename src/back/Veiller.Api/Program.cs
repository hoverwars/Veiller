using System.Text.Json.Serialization;
using Veiller.Api.Auth;
using Veiller.Api.Circles;
using Veiller.Api.Data;

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

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();

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

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program;
