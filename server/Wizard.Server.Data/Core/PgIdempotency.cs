using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2, spec 12 1.3절, ARCHITECTURE 6.3절. request 행을 **먼저** INSERT … ON CONFLICT DO NOTHING 한다(조회를 먼저 하지 않는다).
    //   1행이 들어갔다 → body 를 돌리고 결과를 그 행에 저장하고 COMMIT. 같은 키의 동시 요청은 이 INSERT 에서 앞의 트랜잭션이 끝나기를 기다린다.
    //   0행이다(이미 있다) → 그 행을 FOR UPDATE 로 잡는다. kind 나 지문이 다르면 Conflict(IdempotencyConflict). 같으면 저장된 결과(body 를 부르지 않는다).
    // body 가 던지면(DB 오류, 강제 종료) 트랜잭션이 되돌려져 request 행도 남지 않는다. 같은 키로 다시 보내면 새로 돈다(T-IDEM-03).
    // 규칙상 거절은 body 가 결과 값으로 돌려준다. 그 결과도 저장되고 커밋된다(1.3절 경계).
    public sealed class PgIdempotency : IIdempotency
    {
        private const string InsertSql =
            "INSERT INTO request (account_id, request_id, kind, fingerprint, result, at) " +
            "VALUES (@AccountId, @RequestId, @Kind, @Fingerprint, NULL, @At) " +
            "ON CONFLICT (account_id, request_id) DO NOTHING";

        private const string SelectForUpdateSql =
            "SELECT kind, fingerprint, result::text AS result FROM request " +
            "WHERE account_id = @AccountId AND request_id = @RequestId FOR UPDATE";

        private const string StoreResultSql =
            "UPDATE request SET result = @Result::jsonb WHERE account_id = @AccountId AND request_id = @RequestId";

        private const string PurgeSql = "DELETE FROM request WHERE at < @Cutoff";

        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IClock _clock;
        private readonly JsonSerializerOptions _json;

        public PgIdempotency(IUnitOfWorkFactory uowFactory, IClock clock)
            : this(uowFactory, clock, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        {
        }

        // 결과는 jsonb 로 저장한다(T-20: request.result 는 응답 JSON 그대로). 다른 직렬화 설정이 필요하면 이 생성자로 준다
        public PgIdempotency(IUnitOfWorkFactory uowFactory, IClock clock, JsonSerializerOptions json)
        {
            _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public async Task<IdempotentResult<TResult>> RunAsync<TResult>(long accountId, Guid requestId, string kind, ReadOnlyMemory<byte> bodyFingerprint,
            Func<IUnitOfWork, Task<TResult>> body, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(kind))
            {
                throw new ArgumentException("kind is required", nameof(kind));
            }
            if (bodyFingerprint.IsEmpty)
            {
                throw new ArgumentException("body fingerprint is required", nameof(bodyFingerprint));
            }
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }
            byte[] fingerprint = bodyFingerprint.ToArray();

            // 두 번째 시도는 "0행이었는데 그 사이 정리 작업이 행을 지운" 경우뿐이다. 그때는 처음 온 요청처럼 다시 넣는다
            for (int attempt = 0; ; attempt++)
            {
                await using IUnitOfWork uow = await _uowFactory.BeginAsync(ct).ConfigureAwait(false);
                var key = new { AccountId = accountId, RequestId = requestId };
                int inserted = await uow.Connection.ExecuteAsync(new CommandDefinition(InsertSql,
                    new { AccountId = accountId, RequestId = requestId, Kind = kind, Fingerprint = fingerprint, At = _clock.UtcNow },
                    uow.Transaction, cancellationToken: ct)).ConfigureAwait(false);

                if (inserted == 1)
                {
                    TResult value = await body(new BodyUnitOfWork(uow)).ConfigureAwait(false);
                    string json = JsonSerializer.Serialize(value, _json);
                    await uow.Connection.ExecuteAsync(new CommandDefinition(StoreResultSql,
                        new { AccountId = accountId, RequestId = requestId, Result = json },
                        uow.Transaction, cancellationToken: ct)).ConfigureAwait(false);
                    await uow.CommitAsync(ct).ConfigureAwait(false);
                    return new IdempotentResult<TResult>(false, false, value);
                }

                StoredRequest? stored = await uow.Connection.QuerySingleOrDefaultAsync<StoredRequest>(new CommandDefinition(SelectForUpdateSql,
                    key, uow.Transaction, cancellationToken: ct)).ConfigureAwait(false);
                if (stored == null)
                {
                    if (attempt == 0)
                    {
                        continue;
                    }
                    throw new InvalidOperationException("request row vanished twice while waiting for it");
                }
                if (!string.Equals(stored.Kind, kind, StringComparison.Ordinal) || !stored.Fingerprint.AsSpan().SequenceEqual(fingerprint))
                {
                    return new IdempotentResult<TResult>(true, false, default!);
                }
                if (stored.Result == null)
                {
                    // 결과 없이 커밋된 행은 이 코드가 만들지 않는다(결과 저장과 커밋이 한 트랜잭션)
                    throw new InvalidOperationException("committed request row has no stored result");
                }
                TResult replayed = JsonSerializer.Deserialize<TResult>(stored.Result, _json)!;
                return new IdempotentResult<TResult>(false, true, replayed);
            }
        }

        // 30일 보관(spec 12 8.4절, T-IDEM-05). 보관 기간은 부르는 쪽(정리 작업)이 설정에서 정한다. 지운 행 수를 돌려준다
        public Task<int> PurgeOlderThanAsync(IUnitOfWork uow, DateTime cutoffUtc, CancellationToken ct)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            return uow.Connection.ExecuteAsync(new CommandDefinition(PurgeSql, new { Cutoff = cutoffUtc }, uow.Transaction, cancellationToken: ct));
        }
    }
}
