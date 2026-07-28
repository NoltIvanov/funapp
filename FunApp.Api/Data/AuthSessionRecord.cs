namespace FunApp.Api.Data;

public sealed record AuthSessionRecord(string Token, DateTimeOffset ExpiresAtUtc);
