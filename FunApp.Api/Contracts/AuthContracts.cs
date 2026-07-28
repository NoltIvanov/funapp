namespace FunApp.Api.Contracts;

public sealed record GoogleAuthorizationCodeExchangeRequest(
    string AuthorizationCode,
    string CodeVerifier,
    string RedirectUri,
    string ClientId,
    string? Platform);

public sealed record AuthResponse(
    string SessionToken,
    DateTimeOffset ExpiresAtUtc,
    UserResponse User);

public sealed record HealthResponse(string Status, DateTimeOffset Utc);

public sealed record UserResponse(
    string Id,
    string Provider,
    string Name,
    string Email,
    string? Picture);
