using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. PostgreSQL 트랜잭션 하나
    public interface IUnitOfWork : IAsyncDisposable
    {
        NpgsqlConnection Connection { get; }
        NpgsqlTransaction Transaction { get; }
        Task CommitAsync(CancellationToken ct);
    }
}
