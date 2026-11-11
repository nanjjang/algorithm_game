using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2
    public interface IIdempotency
    {
        // 한 트랜잭션 안에서 request 행을 먼저 INSERT … ON CONFLICT DO NOTHING 한다(spec 12 1.3절. 조회를 먼저 하지 않는다).
        // bodyFingerprint = 요청 본문 정규 JSON 의 SHA-256(kind, 경로 인자 포함).
        Task<IdempotentResult<TResult>> RunAsync<TResult>(long accountId, Guid requestId, string kind, ReadOnlyMemory<byte> bodyFingerprint,
        Func<IUnitOfWork, Task<TResult>> body, CancellationToken ct);
    }
}
