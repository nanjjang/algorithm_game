using System;
using System.Threading.Tasks;
using Dapper;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2, spec 12 1.3절 3번: 여러 계정 행은 **계정 번호 오름차순**으로 잠근다(교착 방지, T-IDEM-06).
    // 한 줄씩 차례로 잠근다: 한 문장의 ORDER BY … FOR UPDATE 는 실행 계획에 따라 잠그는 순서가 정렬 순서와 다를 수 있다.
    // account 표는 M10-ACC-01, wand 표는 M10-INV-01 의 마이그레이션이 만든다.
    public sealed class PgRowLocks : IRowLocks
    {
        private const string LockAccountSql = "SELECT id FROM account WHERE id = @Id FOR UPDATE";

        private const string LockWandSql =
            "SELECT id AS Id, owner_id AS OwnerId, character AS Character, stage AS Stage, floor_peak AS FloorPeak, record_peak AS RecordPeak, " +
            "drop_count AS DropCount, aura AS Aura, slots_extra AS SlotsExtra, carve_bonus AS CarveBonus, mutations::text AS Mutations, " +
            "pending_offer::text AS PendingOffer, loadout::text AS Loadout, skin_id AS SkinId, show_scars AS ShowScars, origin AS Origin, " +
            "owner_count AS OwnerCount, trade_locked_until AS TradeLockedUntil, lock_state AS LockState, pending_run_id AS PendingRunId, " +
            "debt_locked AS DebtLocked, acquired_at AS AcquiredAt, version AS Version " +
            "FROM wand WHERE id = @Id FOR UPDATE";

        public async Task LockAccountsAsync(IUnitOfWork uow, params long[] accountIds)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            if (accountIds == null || accountIds.Length == 0)
            {
                throw new ArgumentException("at least one account id", nameof(accountIds));
            }
            long[] ids = (long[])accountIds.Clone();
            Array.Sort(ids);   // long 의 오름차순은 동률이 없는 완전 순서다
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0 && ids[i] == ids[i - 1])
                {
                    continue;
                }
                long? found = await uow.Connection.ExecuteScalarAsync<long?>(new CommandDefinition(LockAccountSql, new { Id = ids[i] }, uow.Transaction))
                    .ConfigureAwait(false);
                if (found == null)
                {
                    throw new RowNotFoundException("account", ids[i]);
                }
            }
        }

        public async Task<WandRow> LockWandAsync(IUnitOfWork uow, long wandId)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            WandRow? row = await uow.Connection.QuerySingleOrDefaultAsync<WandRow>(new CommandDefinition(LockWandSql, new { Id = wandId }, uow.Transaction))
                .ConfigureAwait(false);
            return row ?? throw new RowNotFoundException("wand", wandId);
        }
    }
}
