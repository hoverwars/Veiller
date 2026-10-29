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

## Firebase authentication

The auth routes (`/api/auth/*`) rely on Firebase Authentication (free Spark plan, email/password provider).
Without the configuration below the API refuses to start.

1. Store the Project ID and the Web API key (Firebase console > Project settings > General):

   ```bash
   cd src/back/Veiller.Api
   dotnet user-secrets set "Firebase:ProjectId" "<project-id>"
   dotnet user-secrets set "Firebase:WebApiKey" "<web-api-key>"
   ```

2. Download a service account key (Project settings > Service accounts > Generate new private key), keep it
   **outside the repository** and point to it before starting the API:

   ```powershell
   $env:GOOGLE_APPLICATION_CREDENTIALS = "C:\Users\<you>\.secrets\veiller-firebase.json"
   ```

### Routes

| Route                          | Auth   | Description                                   |
|--------------------------------|--------|-----------------------------------------------|
| `POST /api/auth/register`      | -      | Create an account (`fullName`, `email`, `password`, `role`: `caregiver`/`elder`) |
| `POST /api/auth/login`         | -      | Sign in, returns the user and the tokens      |
| `POST /api/auth/refresh`       | -      | New ID token from a refresh token             |
| `GET /api/auth/me`             | Bearer | Current user                                  |
| `POST /api/auth/me/onboarding` | Bearer | Mark the onboarding as completed              |
| `POST /api/auth/logout`        | Bearer | Revoke the refresh tokens                     |

In Swagger, paste the `idToken` returned by `login` in the **Authorize** dialog.

### Deployment

GitHub environment `Development` needs the variable `FIREBASE_PROJECT_ID` and the secrets
`FIREBASE_WEB_API_KEY` and `FIREBASE_SERVICE_ACCOUNT` (full content of the service account JSON).
