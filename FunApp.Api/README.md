# FunApp API

ASP.NET Core backend for production OAuth code exchange. The app sends the Google authorization code and PKCE verifier here; the backend exchanges them with Google and returns an app session token.

## Local setup

Set the Google desktop OAuth secret outside git:

```bash
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google-desktop-client-secret>" --project FunApp.Api/FunApp.Api.csproj
```

Run the API:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --project FunApp.Api/FunApp.Api.csproj --urls http://127.0.0.1:5080
```

Health check:

```bash
curl http://127.0.0.1:5080/health
```

Make the MAUI app use this backend for Google login:

```bash
export FUNAPP_API_BASE_URL=http://127.0.0.1:5080
dotnet build MauiApp1/MauiApp1.csproj -f net10.0-maccatalyst
```

The variable must be visible to the app process when it starts. On Windows, set `FUNAPP_API_BASE_URL` for the app process to your API URL. For a production publish, point it at the deployed HTTPS API.

## Production config

Use environment variables or your hosting secret store:

```bash
Authentication__Google__ClientSecret=<google-desktop-client-secret>
ConnectionStrings__FunApp="Data Source=/var/lib/funapp/funapp.db"
```

`Authentication:Google:MobileClientId` and `Authentication:Google:DesktopClientId` are public OAuth client IDs, so they can live in appsettings. The secret must not.

## Auth endpoints

`POST /auth/google/exchange`

```json
{
  "authorizationCode": "<code-from-google>",
  "codeVerifier": "<pkce-code-verifier>",
  "redirectUri": "<same-redirect-uri-used-for-google-auth>",
  "clientId": "<same-google-client-id-used-for-google-auth>",
  "platform": "windows"
}
```

Response:

```json
{
  "sessionToken": "<funapp-session-token>",
  "expiresAtUtc": "2026-08-27T12:00:00+00:00",
  "user": {
    "id": "<google-sub>",
    "provider": "Google",
    "name": "Name",
    "email": "email@example.com",
    "picture": "https://..."
  }
}
```

`GET /me`

```bash
curl -H "Authorization: Bearer <funapp-session-token>" http://127.0.0.1:5080/me
```
