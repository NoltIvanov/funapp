using System.Data.Common;

namespace FunApp.Api.Infrastructure;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
