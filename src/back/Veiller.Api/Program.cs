using Veiller.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Veiller API", Version = "v1" });
});
builder.Services.AddBearerSwagger();
builder.Services.AddValidation();
builder.Services.AddFirebaseAuth(builder.Configuration);

var app = builder.Build();

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

await app.RunAsync();
