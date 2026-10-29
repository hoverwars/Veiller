using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Veiller.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddFirebaseAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FirebaseOptions>()
            .Bind(configuration.GetSection(FirebaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Default credentials: GOOGLE_APPLICATION_CREDENTIALS points to the service account JSON.
        services.AddSingleton(_ => FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault(),
        }));
        services.AddSingleton(sp => FirebaseAuth.GetAuth(sp.GetRequiredService<FirebaseApp>()));

        services.AddHttpClient<FirebaseIdentityClient>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FirebaseOptions>>((jwt, firebase) =>
            {
                var projectId = firebase.Value.ProjectId;
                jwt.Authority = $"https://securetoken.google.com/{projectId}";
                jwt.TokenValidationParameters.ValidIssuer = jwt.Authority;
                jwt.TokenValidationParameters.ValidAudience = projectId;
            });
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddBearerSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Firebase ID token obtained from /api/auth/login.",
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                }] = [],
            });
        });
        return services;
    }
}
