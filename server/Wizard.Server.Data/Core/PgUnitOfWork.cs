using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. 커밋하지 않고 닫으면 되돌린다(spec 12 1.3절 경계: 롤백되면 request 행도 남지 않아 다시 보낼 수 있다).
    internal sealed class PgUnitOfWork : IUnitOfWork
    {
        private const string AliveSql = "SELECT 1";

        private bool _committed;
        private bool _disposed;

        public PgUnitOfWork(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        public NpgsqlConnection Connection { get; }

        public NpgsqlTransaction Transaction { get; }

        public async Task CommitAsync(CancellationToken ct)
        {
            if (_disposed || _committed)
            {
                throw new InvalidOperationException("unit of work already completed");
            }
            // 앞선 문장이 실패해 취소된 트랜잭션에 COMMIT 을 보내면 PostgreSQL 은 오류 없이 ROLLBACK 으로 끝내고 Npgsql 도 던지지 않는다.
            // 예외를 잡고 계속 간 유스케이스가 "커밋됐다"고 믿지 않도록, 커밋 전에 트랜잭션이 살아 있는지 한 번 묻는다.
            // 취소된 트랜잭션이면 이 문장이 PostgresException(25P02 in_failed_sql_transaction)으로 던진다(T-IDEM-10).
            await using (var probe = new NpgsqlCommand(AliveSql, Connection, Transaction))
            {
                await probe.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            await Transaction.CommitAsync(ct).ConfigureAwait(false);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                // 커밋하지 않은 트랜잭션은 Dispose 가 되돌린다. 끊긴 연결이면 서버가 이미 되돌렸다
                await Transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await Connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
