using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2: 재화와 물건의 유일한 변경 통로. 잔액(수량) 행을 갱신해 잠그고, 같은 트랜잭션에서 원장에 한 줄을 더한다.
    // 서버 코드에서 currency_balance·item_stack·두 원장 표에 쓰는 SQL 은 이 파일에만 있다(LedgerOnlyWriterTests 가 확인한다).
    //
    // pending [임시, spec 12 9.5절의 대조 규칙과 spec 09 9.3~9.4절에서 읽음]: pending 줄은 검증 대기분(amount_pending, qty_pending)을,
    // 나머지 줄은 확정분(amount, qty)을 바꾼다. 검증 통과는 대기 −x 줄과 확정 +x 줄 두 줄이다.
    // 대조: 구분마다 SUM(delta WHERE NOT pending) = amount, SUM(delta WHERE pending) = amount_pending.
    //
    // 없는 잔액 행은 0 으로 먼저 만든 뒤 UPDATE 한다. INSERT … ON CONFLICT DO UPDATE 한 줄로 쓰면 새로 넣을 행(음수)의 CHECK 가
    // 충돌 판정보다 먼저 검사돼 잔액이 있어도 차감이 거절된다.
    // 음수 결과는 DB CHECK 가 막는다(qty ≥ 0, 유료 구분 밖의 amount ≥ 0). 위반은 PostgresException(23514)이고 트랜잭션은 취소된다.
    public sealed class PgLedger : ILedger
    {
        private const string EnsureBalanceSql =
            "INSERT INTO currency_balance (account_id, currency, bucket, amount, amount_pending) " +
            "VALUES (@AccountId, @Currency, @Bucket, 0, 0) ON CONFLICT (account_id, currency, bucket) DO NOTHING";

        private const string AddAmountSql =
            "UPDATE currency_balance SET amount = amount + @Delta " +
            "WHERE account_id = @AccountId AND currency = @Currency AND bucket = @Bucket RETURNING amount";

        private const string AddAmountPendingSql =
            "UPDATE currency_balance SET amount_pending = amount_pending + @Delta " +
            "WHERE account_id = @AccountId AND currency = @Currency AND bucket = @Bucket RETURNING amount_pending";

        private const string InsertCurrencyLedgerSql =
            "INSERT INTO currency_ledger (account_id, currency, bucket, delta, balance_after, pending, reason, ref_type, ref_id, request_id, at) " +
            "VALUES (@AccountId, @Currency, @Bucket, @Delta, @BalanceAfter, @Pending, @Reason, @RefType, @RefId, @RequestId, @At)";

        // item_stack 표는 M10-INV-01 의 마이그레이션이 만든다(CONTRACTS 12.2절, spec 12 8.2절의 열)
        private const string EnsureStackSql =
            "INSERT INTO item_stack (account_id, item_code) VALUES (@AccountId, @ItemCode) ON CONFLICT (account_id, item_code) DO NOTHING";

        private const string AddQtySql =
            "UPDATE item_stack SET qty = qty + @Delta WHERE account_id = @AccountId AND item_code = @ItemCode RETURNING qty";

        private const string AddQtyPendingSql =
            "UPDATE item_stack SET qty_pending = qty_pending + @Delta WHERE account_id = @AccountId AND item_code = @ItemCode RETURNING qty_pending";

        private const string InsertItemLedgerSql =
            "INSERT INTO item_ledger (account_id, item_ref, delta, pending, reason, ref_type, ref_id, request_id, at) " +
            "VALUES (@AccountId, @ItemRef, @Delta, @Pending, @Reason, @RefType, @RefId, @RequestId, @At)";

        // 별빛 두 구분을 같은 순서(bucket 이름순: free → paid)로 잠근다
        private const string LockStarlightSql =
            "SELECT bucket, amount FROM currency_balance WHERE account_id = @AccountId AND currency = 'starlight' " +
            "AND bucket IN ('free', 'paid') ORDER BY bucket FOR UPDATE";

        private readonly IClock _clock;

        public PgLedger(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        // 바뀐 뒤의 값(pending 이면 amount_pending, 아니면 amount)을 돌려준다
        public async Task<long> AddCurrencyAsync(IUnitOfWork uow, long accountId, CurrencyCode currency, CurrencyBucket bucket, long delta, bool pending,
            LedgerRef reference)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            if (delta == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "a ledger line must change something");
            }
            var key = new { AccountId = accountId, Currency = DbNames.Currency(currency), Bucket = DbNames.Bucket(bucket) };
            await uow.Connection.ExecuteAsync(new CommandDefinition(EnsureBalanceSql, key, uow.Transaction)).ConfigureAwait(false);
            long after = await uow.Connection.ExecuteScalarAsync<long>(new CommandDefinition(pending ? AddAmountPendingSql : AddAmountSql,
                new { key.AccountId, key.Currency, key.Bucket, Delta = delta }, uow.Transaction)).ConfigureAwait(false);
            await uow.Connection.ExecuteAsync(new CommandDefinition(InsertCurrencyLedgerSql, new
            {
                key.AccountId,
                key.Currency,
                key.Bucket,
                Delta = delta,
                BalanceAfter = after,
                Pending = pending,
                Reason = DbNames.Reason(reference.Reason),
                reference.RefType,
                reference.RefId,
                reference.RequestId,
                At = _clock.UtcNow,
            }, uow.Transaction)).ConfigureAwait(false);
            return after;
        }

        // itemRef: ItemRefs 의 세 꼴. 겹치는 물건은 item_stack 의 qty(대기면 qty_pending)를 delta 만큼 바꾼다.
        // 지팡이·개체는 그 행을 만들거나 없애는 유스케이스가 같은 트랜잭션에서 +1(생성)·−1(소멸) 줄을 적는다 [임시: 한 줄에 한 개]
        public async Task AddItemAsync(IUnitOfWork uow, long accountId, string itemRef, long delta, bool pending, LedgerRef reference)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            if (delta == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "a ledger line must change something");
            }
            if (ItemRefs.TryGetStackCode(itemRef, out string itemCode))
            {
                var key = new { AccountId = accountId, ItemCode = itemCode };
                await uow.Connection.ExecuteAsync(new CommandDefinition(EnsureStackSql, key, uow.Transaction)).ConfigureAwait(false);
                await uow.Connection.ExecuteScalarAsync<long>(new CommandDefinition(pending ? AddQtyPendingSql : AddQtySql,
                    new { key.AccountId, key.ItemCode, Delta = delta }, uow.Transaction)).ConfigureAwait(false);
            }
            else if (ItemRefs.IsEntity(itemRef))
            {
                if (delta != 1 && delta != -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(delta), "a wand or item instance line is +1 (created) or -1 (destroyed)");
                }
            }
            else
            {
                throw new ArgumentException("item ref must be stack:<code>, wand:<id> or inst:<id>", nameof(itemRef));
            }
            await uow.Connection.ExecuteAsync(new CommandDefinition(InsertItemLedgerSql, new
            {
                AccountId = accountId,
                ItemRef = itemRef,
                Delta = delta,
                Pending = pending,
                Reason = DbNames.Reason(reference.Reason),
                reference.RefType,
                reference.RefId,
                reference.RequestId,
                At = _clock.UtcNow,
            }, uow.Transaction)).ConfigureAwait(false);
        }

        // spec 12 10.1절 별빛 차감: 무상부터. 유료 잔액이 음수(환불 뒤)면 막는다. 두 구분 합이 모자라면 거절.
        // 오류 코드 [임시, OPEN-QUESTIONS]: ErrorCode 에 별빛 전용 값이 없어 막힘 = PaymentBlocked, 부족 = BadRequest
        public async Task SpendStarlightAsync(IUnitOfWork uow, long accountId, long amount, LedgerRef reference)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "spend amount must be positive");
            }
            string currency = DbNames.Currency(CurrencyCode.Starlight);
            string free = DbNames.Bucket(CurrencyBucket.Free);
            string paid = DbNames.Bucket(CurrencyBucket.Paid);
            await uow.Connection.ExecuteAsync(new CommandDefinition(EnsureBalanceSql,
                new[] { new { AccountId = accountId, Currency = currency, Bucket = free }, new { AccountId = accountId, Currency = currency, Bucket = paid } },
                uow.Transaction)).ConfigureAwait(false);
            List<BucketAmount> rows = (await uow.Connection.QueryAsync<BucketAmount>(new CommandDefinition(LockStarlightSql,
                new { AccountId = accountId }, uow.Transaction)).ConfigureAwait(false)).ToList();
            long freeAmount = rows.Single(r => r.Bucket == free).Amount;
            long paidAmount = rows.Single(r => r.Bucket == paid).Amount;
            if (paidAmount < 0)
            {
                throw new LedgerException(ErrorCode.PaymentBlocked, "starlight is blocked while the paid balance is negative after a refund");
            }
            if (freeAmount + paidAmount < amount)
            {
                throw new LedgerException(ErrorCode.BadRequest, "not enough starlight");
            }
            long useFree = Math.Min(freeAmount, amount);
            long usePaid = amount - useFree;
            if (useFree > 0)
            {
                await AddCurrencyAsync(uow, accountId, CurrencyCode.Starlight, CurrencyBucket.Free, -useFree, false, reference).ConfigureAwait(false);
            }
            if (usePaid > 0)
            {
                await AddCurrencyAsync(uow, accountId, CurrencyCode.Starlight, CurrencyBucket.Paid, -usePaid, false, reference).ConfigureAwait(false);
            }
        }
    }
}
