using Dapper;

namespace FunApp.Api.Infrastructure;

public sealed class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                provider TEXT NOT NULL,
                provider_user_id TEXT NOT NULL,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                picture_url TEXT NULL,
                created_at_utc INTEGER NOT NULL,
                updated_at_utc INTEGER NOT NULL,
                UNIQUE(provider, provider_user_id)
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                token_hash TEXT NOT NULL UNIQUE,
                created_at_utc INTEGER NOT NULL,
                expires_at_utc INTEGER NOT NULL,
                revoked_at_utc INTEGER NULL,
                FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_sessions_token_hash
                ON sessions(token_hash);

            CREATE INDEX IF NOT EXISTS ix_sessions_user_id
                ON sessions(user_id);
            """,
            cancellationToken: cancellationToken));
    }
}
