using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;

namespace Wizard.Server.Data.Accounts
{
    // mail_outbox 표의 저장소(spec 12 2.8절 발송 대기열 상태 기계). 시도 간격(1·5·15분)과 시도 상한(4회)은 App 이 정한다.
    //   Queued ─(보냄)→ Sent
    //   Queued/Failed ─(실패, 다시 시도)→ Failed(next_try_at)       ← Failed 도 다시 꺼내진다
    //   Queued/Failed ─(상한, 재시도 불가, 코드 만료)→ Dropped
    //   Sent ─(사업자의 반송·거절 사건)→ Dropped                      ← DECISIONS-TECH T-56. sent_at 과 provider_ref 는 남는다
    // 상태 값은 spec 12 8.1절의 넷뿐이다. "배달됨"은 따로 적지 않고(Sent 그대로), "반송됨"은 Dropped 이면서 sent_at 이 있는 줄이다.
    // 발송 작업 여럿이 같은 줄을 보내지 않도록 꺼낼 때 FOR UPDATE SKIP LOCKED 로 잡는다.
    public sealed class PgMailOutboxStore
    {
        private const string Columns =
            "id AS Id, kind AS Kind, account_id AS AccountId, status AS Status, tries AS Tries, provider_ref AS ProviderRef, " +
            "created_at AS CreatedAt, sent_at AS SentAt, next_try_at AS NextTryAt, token_id AS TokenId";

        private const string EnqueueSql =
            "INSERT INTO mail_outbox (kind, account_id, status, tries, created_at, next_try_at, token_id) " +
            "VALUES (@Kind, @AccountId, 'queued', 0, @At, @At, @TokenId) RETURNING id";

        private const string LockDueSql =
            "SELECT " + Columns + " FROM mail_outbox WHERE status IN ('queued', 'failed') AND next_try_at <= @Now " +
            "ORDER BY next_try_at, id LIMIT @Limit FOR UPDATE SKIP LOCKED";

        private const string GetSql = "SELECT " + Columns + " FROM mail_outbox WHERE id = @Id";

        private const string MarkSentSql =
            "UPDATE mail_outbox SET status = 'sent', tries = tries + 1, provider_ref = @ProviderRef, sent_at = @At, next_try_at = NULL " +
            "WHERE id = @Id AND status IN ('queued', 'failed')";

        private const string MarkRetrySql =
            "UPDATE mail_outbox SET status = 'failed', tries = tries + 1, next_try_at = @NextTryAt " +
            "WHERE id = @Id AND status IN ('queued', 'failed')";

        // attempted = 이번에 실제로 보내 보고 버리는가(시도 수에 넣는다). 코드가 만료돼 보내지 않고 버리면 false
        private const string MarkDroppedSql =
            "UPDATE mail_outbox SET status = 'dropped', tries = tries + CASE WHEN @Attempted THEN 1 ELSE 0 END, next_try_at = NULL " +
            "WHERE id = @Id AND status IN ('queued', 'failed')";

        // 반송 사건은 사업자의 참조 값으로 온다. 같은 사건이 두 번 와도 한 번만 바뀐다(이미 Dropped 면 0줄).
        // 참조 값은 사업자가 메일마다 다르게 주지만, 겹쳐도 한 줄만 바꾸도록 가장 오래된 Sent 줄 하나를 고른다
        private const string MarkBouncedSql =
            "UPDATE mail_outbox SET status = 'dropped' WHERE id = (" +
            "SELECT id FROM mail_outbox WHERE provider_ref = @ProviderRef AND status = 'sent' ORDER BY id LIMIT 1 FOR UPDATE) " +
            "RETURNING " + Columns;

        // 그 계정의 주소로 보낸 메일이 마지막으로 반송된 것의 발송 시각(30일 보관 안에서). 다시 보낼지의 판정은 App
        private const string LastBouncedSentAtSql =
            "SELECT max(sent_at) FROM mail_outbox WHERE account_id = @AccountId AND status = 'dropped' AND sent_at IS NOT NULL";

        // 서버 전체 하루 발송 상한(2.8절 500통). "오늘 0시"(KST [임시])는 호출자가 since 로 준다.
        // 사업자가 받은 메일을 센다: 뒤에 반송돼 Dropped 가 된 줄도 그날의 한 통이다(T-56. 반송이 한도를 돌려주지 않는다)
        private const string CountSentSinceSql = "SELECT count(*) FROM mail_outbox WHERE sent_at >= @Since";

        private const string CountsSinceSql =
            "SELECT count(*) FILTER (WHERE status = 'queued') AS Queued, count(*) FILTER (WHERE status = 'sent') AS Sent, " +
            "count(*) FILTER (WHERE status = 'failed') AS Failed, count(*) FILTER (WHERE status = 'dropped') AS Dropped " +
            "FROM mail_outbox WHERE created_at >= @Since";

        // 8.4절: 30일 뒤 삭제
        private const string DeleteBeforeSql = "DELETE FROM mail_outbox WHERE created_at < @Cutoff";

        public Task<long> EnqueueAsync(IUnitOfWork uow, MailOutboxKind kind, long? accountId, long? tokenId, DateTime at) =>
            StoreSql.Scalar<long>(uow, EnqueueSql, new { Kind = AccountDbNames.MailKind(kind), AccountId = accountId, TokenId = tokenId, At = StoreSql.Utc(at) });

        public Task<IReadOnlyList<MailOutboxRow>> LockDueAsync(IUnitOfWork uow, DateTime now, int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
            return StoreSql.Many<MailOutboxRow>(uow, LockDueSql, new { Now = StoreSql.Utc(now), Limit = limit });
        }

        public Task<MailOutboxRow?> GetAsync(IUnitOfWork uow, long id) => StoreSql.One<MailOutboxRow>(uow, GetSql, new { Id = id });

        // 끝난 줄(sent, dropped)은 바꾸지 않는다: false
        public Task<bool> MarkSentAsync(IUnitOfWork uow, long id, string? providerRef, DateTime at) =>
            StoreSql.Exactly1(uow, MarkSentSql, new { Id = id, ProviderRef = providerRef, At = StoreSql.Utc(at) });

        public Task<bool> MarkRetryAsync(IUnitOfWork uow, long id, DateTime nextTryAt) =>
            StoreSql.Exactly1(uow, MarkRetrySql, new { Id = id, NextTryAt = StoreSql.Utc(nextTryAt) });

        public Task<bool> MarkDroppedAsync(IUnitOfWork uow, long id, bool attempted) =>
            StoreSql.Exactly1(uow, MarkDroppedSql, new { Id = id, Attempted = attempted });

        // 없는 참조 값이거나 이미 닫힌 줄이면 null. 바뀐 줄을 돌려준다(누구의 메일인지는 AccountId)
        public Task<MailOutboxRow?> MarkBouncedAsync(IUnitOfWork uow, string providerRef)
        {
            if (string.IsNullOrEmpty(providerRef))
            {
                throw new ArgumentException("provider reference is required", nameof(providerRef));
            }
            return StoreSql.One<MailOutboxRow>(uow, MarkBouncedSql, new { ProviderRef = providerRef });
        }

        public async Task<DateTime?> LastBouncedSentAtAsync(IUnitOfWork uow, long accountId)
        {
            StoreSql.Require(uow);
            return await uow.Connection.ExecuteScalarAsync<DateTime?>(
                new CommandDefinition(LastBouncedSentAtSql, new { AccountId = accountId }, uow.Transaction)).ConfigureAwait(false);
        }

        public Task<int> CountSentSinceAsync(IUnitOfWork uow, DateTime since) =>
            StoreSql.Count(uow, CountSentSinceSql, new { Since = StoreSql.Utc(since) });

        public async Task<MailOutboxCounts> CountsSinceAsync(IUnitOfWork uow, DateTime since) =>
            await StoreSql.One<MailOutboxCounts>(uow, CountsSinceSql, new { Since = StoreSql.Utc(since) }).ConfigureAwait(false) ?? new MailOutboxCounts();

        public Task<int> DeleteBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteBeforeSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
