using FunApp.Api.Contracts;

namespace FunApp.Api.Data;

public sealed class UserRecord
{
    public const string SelectSql =
        """
        SELECT
            u.id AS AppUserId,
            u.provider AS Provider,
            u.provider_user_id AS ProviderUserId,
            u.name AS Name,
            u.email AS Email,
            u.picture_url AS PictureUrl,
            u.created_at_utc AS CreatedAtUtc,
            u.updated_at_utc AS UpdatedAtUtc
        """;

    public string AppUserId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ProviderUserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PictureUrl { get; init; }
    public long CreatedAtUtc { get; init; }
    public long UpdatedAtUtc { get; init; }

    public UserResponse ToResponse()
    {
        return new UserResponse(ProviderUserId, Provider, Name, Email, PictureUrl);
    }
}
