namespace FunApp.Api.Auth;

public sealed record ExternalUserProfile(
    string Provider,
    string ProviderUserId,
    string Name,
    string Email,
    string? PictureUrl);
