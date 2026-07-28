using Dapper;
using Microsoft.Data.Sqlite;

namespace FunApp.Api.Infrastructure;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("FunApp")
            ?? "Data Source=funapp.db";
    }

    public async Task<System.Data.Common.DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "PRAGMA foreign_keys = ON;",
            cancellationToken: cancellationToken));

        return connection;
    }
}
