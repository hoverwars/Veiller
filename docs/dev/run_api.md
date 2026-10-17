# Run the API

Quick guide to start the Veiller backend locally.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Start

From the repository root:

```bash
cd src/back/Veiller.Api
dotnet run --launch-profile https
```

The `https` profile sets `ASPNETCORE_ENVIRONMENT=Development` but it is not necessary to use this profile

If the HTTPS dev certificate is not trusted yet, run once: `dotnet dev-certs https --trust`.

## URLs

| What    | URL                                    |
|---------|----------------------------------------|
| API     | https://localhost:5151 / http://localhost:5150 |
| Swagger | https://localhost:5151/swagger         |
| OpenAPI | https://localhost:5151/swagger/v1/swagger.json |
| Health  | https://localhost:5151/api/health      |
