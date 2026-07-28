using Dapper;
using FunApp.Api.Auth;
using FunApp.Api.Infrastructure;

namespace FunApp.Api.Data;

public sealed class UserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserRecord> UpsertAsync(
        ExternalUserProfile profile,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var existingUserId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT id
            FROM users
            WHERE provider = @Provider
              AND provider_user_id = @ProviderUserId;
            """,
            profile,
            cancellationToken: cancellationToken));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (string.IsNullOrWhiteSpace(existingUserId))
        {
            existingUserId = Guid.CreateVersion7().ToString("N");

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO users (
                    id,
                    provider,
                    provider_user_id,
                    name,
                    email,
                    picture_url,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (
                    @Id,
                    @Provider,
                    @ProviderUserId,
                    @Name,
                    @Email,
                    @PictureUrl,
                    @CreatedAtUtc,
                    @UpdatedAtUtc
                );
                """,
                new
                {
                    Id = existingUserId,
                    profile.Provider,
                    profile.ProviderUserId,
                    profile.Name,
                    profile.Email,
                    profile.PictureUrl,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE users
                SET name = @Name,
                    email = @Email,
                    picture_url = @PictureUrl,
                    updated_at_utc = @UpdatedAtUtc
                WHERE id = @Id;
                """,
                new
                {
                    Id = existingUserId,
                    profile.Name,
                    profile.Email,
                    profile.PictureUrl,
                    UpdatedAtUtc = now,
                },
                cancellationToken: cancellationToken));
        }

        return await connection.QuerySingleAsync<UserRecord>(new CommandDefinition(
            UserRecord.SelectSql +
            """
            FROM users AS u
            WHERE u.id = @Id;
            """,
            new { Id = existingUserId },
            cancellationToken: cancellationToken));
    }
}
