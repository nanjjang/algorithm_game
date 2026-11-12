using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // sanction 표의 저장소(spec 12 8.2절 운영). 이력은 지우지 않는다. 해제는 until 을 그 시각으로 당긴다.
    // account.sanction_until 캐시는 호출자가 같은 트랜잭션에서 PgAccountStore.SetSanctionUntilAsync 로 맞춘다
    public sealed class PgSanctionStore
    {
        private const string Columns =
            "id AS Id, account_id AS AccountId, kind AS Kind, reason AS Reason, \"from\" AS From, until AS Until, by AS By";

        private const string InsertSql =
            "INSERT INTO sanction (account_id, kind, reason, \"from\", until, by) VALUES (@AccountId, @Kind, @Reason, @From, @Until, @By) RETURNING id";

        // 지금 걸려 있는 제재: 시작했고, 영구이거나 아직 끝나지 않았다
        private const string ListActiveSql =
            "SELECT " + Columns + " FROM sanction WHERE account_id = @AccountId AND \"from\" <= @Now AND (until IS NULL OR until > @Now) ORDER BY id";

        private const string ListAllSql = "SELECT " + Columns + " FROM sanction WHERE account_id = @AccountId ORDER BY id";

        private const string GetSql = "SELECT " + Columns + " FROM sanction WHERE id = @Id";

        // 해제: 아직 끝나지 않은 제재만 그 시각에 끝낸다
        private const string EndSql = "UPDATE sanction SET until = @At WHERE id = @Id AND (until IS NULL OR until > @At) AND \"from\" < @At";

        public Task<long> InsertAsync(IUnitOfWork uow, NewSanction sanction)
        {
            if (sanction == null)
            {
                throw new ArgumentNullException(nameof(sanction));
            }
            return StoreSql.Scalar<long>(uow, InsertSql, new
            {
                sanction.AccountId,
                Kind = AccountDbNames.Sanction(sanction.Kind),
                sanction.Reason,
                From = StoreSql.Utc(sanction.From),
                Until = StoreSql.UtcOrNull(sanction.Until),
                sanction.By,
            });
        }

        public Task<IReadOnlyList<SanctionRow>> ListActiveAsync(IUnitOfWork uow, long accountId, DateTime now) =>
            StoreSql.Many<SanctionRow>(uow, ListActiveSql, new { AccountId = accountId, Now = StoreSql.Utc(now) });

        public Task<IReadOnlyList<SanctionRow>> ListAllAsync(IUnitOfWork uow, long accountId) =>
            StoreSql.Many<SanctionRow>(uow, ListAllSql, new { AccountId = accountId });

        public Task<SanctionRow?> GetAsync(IUnitOfWork uow, long sanctionId) => StoreSql.One<SanctionRow>(uow, GetSql, new { Id = sanctionId });

        public Task<bool> EndAsync(IUnitOfWork uow, long sanctionId, DateTime at) =>
            StoreSql.Exactly1(uow, EndSql, new { Id = sanctionId, At = StoreSql.Utc(at) });
    }
}
