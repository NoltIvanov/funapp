using Dapper;
using FunApp.Api.Auth;
using FunApp.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FunApp.Api.Data;

public sealed class SessionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly AuthSessionOptions _options;

    public SessionRepository(
        IDbConnectionFactory connectionFactory,
        IOptions<AuthSessionOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public async Task<AuthSessionRecord> CreateAsync(
        string appUserId,
        CancellationToken cancellationToken)
    {
        var token = SessionToken.Create();
        var tokenHash = SessionToken.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(_options.LifetimeDays);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sessions (
                id,
                user_id,
                token_hash,
                created_at_utc,
                expires_at_utc
            )
            VALUES (
                @Id,
                @UserId,
                @TokenHash,
                @CreatedAtUtc,
                @ExpiresAtUtc
            );
            """,
            new
            {
                Id = Guid.CreateVersion7().ToString("N"),
                UserId = appUserId,
                TokenHash = tokenHash,
                CreatedAtUtc = now.ToUnixTimeSeconds(),
                ExpiresAtUtc = expiresAt.ToUnixTimeSeconds(),
            },
            cancellationToken: cancellationToken));

        return new AuthSessionRecord(token, expiresAt);
    }

    public async Task<UserRecord?> FindUserByTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var tokenHash = SessionToken.Hash(token);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            UserRecord.SelectSql +
            """
            FROM sessions AS s
            INNER JOIN users AS u ON u.id = s.user_id
            WHERE s.token_hash = @TokenHash
              AND s.expires_at_utc > @Now
              AND s.revoked_at_utc IS NULL;
            """,
            new { TokenHash = tokenHash, Now = now },
            cancellationToken: cancellationToken));
    }
}
