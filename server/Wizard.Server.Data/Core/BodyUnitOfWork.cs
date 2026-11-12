using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Wizard.Server.Data
{
    // IIdempotency.RunAsync 가 body 에 넘기는 트랜잭션. 커밋과 닫기는 RunAsync 만 한다:
    // body 가 먼저 커밋하면 결과를 request 행에 저장하기 전에 부작용이 확정된다(spec 12 1.3절).
    internal sealed class BodyUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;

        public BodyUnitOfWork(IUnitOfWork inner)
        {
            _inner = inner;
        }

        public NpgsqlConnection Connection => _inner.Connection;

        public NpgsqlTransaction Transaction => _inner.Transaction;

        public Task CommitAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the idempotent body must not commit; IIdempotency.RunAsync commits after storing the result");

        public ValueTask DisposeAsync() => default;
    }
}
