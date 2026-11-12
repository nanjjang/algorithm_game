using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Wizard.Content;
using Wizard.Core;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 2: ILedger 가 잔액·수량과 원장 줄을 한 트랜잭션에서 바꾼다. 별빛은 무상부터(spec 12 10.1절).
    // 대조(spec 12 9.5절, CONTRACTS 12.3절): 원장 합 = 잔액, 생성 − 소멸 = 현재 수량
    public sealed class LedgerTests
    {
        private const long Acct = 7;

        private static LedgerRef Ref(LedgerReason reason = LedgerReason.Admin) => new LedgerRef(reason, "test", "x", null);

        private static Task<long> BalanceAsync(PgTestDatabase db, string currency, string bucket) => db.ScalarAsync<long>(
            "SELECT COALESCE((SELECT amount FROM currency_balance WHERE account_id = @A AND currency = @C AND bucket = @B), 0)",
            new { A = Acct, C = currency, B = bucket });

        [Fact]
        public async Task AddCurrency_WritesBalanceAndOneLedgerLine_InTheSameTransaction()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            Guid r = Guid.NewGuid();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                long after = await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 120, false,
                    new LedgerRef(LedgerReason.RunSettle, "run", "0190aaaa-0000-7000-8000-000000000001", r));
                Assert.Equal(120L, after);
                after = await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, -20, false, Ref(LedgerReason.ShopBuy));
                Assert.Equal(100L, after);
                // 커밋 전에는 다른 연결에 보이지 않는다
                Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
                await uow.CommitAsync(CancellationToken.None);
            }
            Assert.Equal(100L, await BalanceAsync(db, "gold", "earned"));
            var lines = await db.QueryAsync<(long Delta, long BalanceAfter, bool Pending, string Reason, string RefType, string RefId, Guid? RequestId, DateTime At)>(
                "SELECT delta, balance_after, pending, reason, ref_type, ref_id, request_id, at FROM currency_ledger ORDER BY id");
            Assert.Equal(2, lines.Count);
            Assert.Equal((120L, 120L, false, "run_settle", "run", "0190aaaa-0000-7000-8000-000000000001", (Guid?)r),
                (lines[0].Delta, lines[0].BalanceAfter, lines[0].Pending, lines[0].Reason, lines[0].RefType, lines[0].RefId, lines[0].RequestId));
            Assert.Equal((-20L, 100L, "shop_buy"), (lines[1].Delta, lines[1].BalanceAfter, lines[1].Reason));
            Assert.Equal(db.Clock.UtcNow, lines[0].At.ToUniversalTime());   // 서버 시계
        }

        [Fact]
        public async Task DisposeWithoutCommit_RollsBackBalanceAndLedgerTogether()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 50, false, Ref());
                await db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Stack("mat.stone_low"), 3, false, Ref());
            }
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_balance"));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM item_stack"));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM item_ledger"));
        }

        [Fact]
        public async Task PendingLines_ChangeOnlyThePendingColumns()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                Assert.Equal(40L, await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 40, true, Ref(LedgerReason.RunSettle)));
                await db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Stack("mat.stone_low"), 2, true, Ref(LedgerReason.RunSettle));
                await uow.CommitAsync(CancellationToken.None);
            }
            Assert.Equal(0L, await BalanceAsync(db, "gold", "earned"));
            Assert.Equal(40L, await db.ScalarAsync<long>("SELECT amount_pending FROM currency_balance WHERE account_id = @A", new { A = Acct }));
            Assert.Equal((0L, 2L), (await db.QueryAsync<(long, long)>(
                "SELECT qty, qty_pending FROM item_stack WHERE account_id = @A", new { A = Acct })).Single());
            Assert.True(await db.ScalarAsync<bool>("SELECT bool_and(pending) FROM currency_ledger"));
            Assert.True(await db.ScalarAsync<bool>("SELECT bool_and(pending) FROM item_ledger"));
        }

        [Fact]
        public async Task PendingBelowZero_IsCheckViolation()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, -1, true, Ref()));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        [Fact]
        public async Task EntityLines_ArePlusOrMinusOne_AndDoNotTouchStacks()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Wand(11), 1, false, Ref(LedgerReason.ShopBuy));
                await db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Instance(5), 1, false, Ref());
                await db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Instance(5), -1, false, Ref());
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Wand(11), 2, false, Ref()));
                await uow.CommitAsync(CancellationToken.None);
            }
            Assert.Equal(new[] { "inst:5", "inst:5", "wand:11" }, (await db.QueryAsync<string>("SELECT item_ref FROM item_ledger ORDER BY item_ref")).ToArray());
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM item_stack"));
        }

        [Theory]
        [InlineData("gold")]
        [InlineData("stack:")]
        [InlineData("wand:")]
        [InlineData("wand:-1")]
        [InlineData("wand:1x")]
        [InlineData("inst:")]
        [InlineData("")]
        public async Task UnknownItemRefShape_IsRejectedBeforeAnyWrite(string itemRef)
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await Assert.ThrowsAsync<ArgumentException>(() => db.Ledger.AddItemAsync(uow, Acct, itemRef, 1, false, Ref()));
                await uow.CommitAsync(CancellationToken.None);   // 아무것도 쓰지 않았으니 트랜잭션은 살아 있다
            }
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM item_ledger"));
        }

        [Fact]
        public async Task ZeroDelta_IsAProgrammerError()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 0, false, Ref()));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Stack("mat.stone_low"), 0, false, Ref()));
        }

        [Fact]
        public async Task CommitAfterAFailedStatement_Throws_AndNothingIsKept()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 5, false, Ref());
                await Assert.ThrowsAsync<PostgresException>(() =>
                    db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, -6, false, Ref()));
                PostgresException commit = await Assert.ThrowsAsync<PostgresException>(() => uow.CommitAsync(CancellationToken.None));
                Assert.Equal(PostgresErrorCodes.InFailedSqlTransaction, commit.SqlState);
            }
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
        }

        // ---- 별빛(spec 12 10.1절 의사코드) ----

        private static async Task SeedStarlightAsync(PgTestDatabase db, long free, long paid)
        {
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            if (free != 0)
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Starlight, CurrencyBucket.Free, free, false, Ref(LedgerReason.SeasonReward));
            }
            if (paid != 0)
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Starlight, CurrencyBucket.Paid, paid, false, Ref(paid > 0 ? LedgerReason.Payment : LedgerReason.Refund));
            }
            await uow.CommitAsync(CancellationToken.None);
        }

        private static async Task SpendAsync(PgTestDatabase db, long n)
        {
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            await db.Ledger.SpendStarlightAsync(uow, Acct, n, Ref(LedgerReason.ShopBuy));
            await uow.CommitAsync(CancellationToken.None);
        }

        [Theory]
        [InlineData(10, 10, 4, 6, 10)]    // 무상만으로 충분하다
        [InlineData(10, 10, 15, 0, 5)]    // 무상을 다 쓰고 나머지를 유료에서
        [InlineData(0, 10, 3, 0, 7)]      // 무상이 없다
        [InlineData(10, 0, 10, 0, 0)]     // 정확히 무상만큼
        public async Task SpendStarlight_TakesFreeFirst(long free, long paid, long spend, long freeAfter, long paidAfter)
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await SeedStarlightAsync(db, free, paid);
            await SpendAsync(db, spend);
            Assert.Equal(freeAfter, await BalanceAsync(db, "starlight", "free"));
            Assert.Equal(paidAfter, await BalanceAsync(db, "starlight", "paid"));
            Assert.Equal(-spend, await db.ScalarAsync<long>("SELECT COALESCE(sum(delta), 0) FROM currency_ledger WHERE reason = 'shop_buy'"));
            // 0 줄은 남기지 않는다
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger WHERE delta = 0"));
        }

        [Fact]
        public async Task SpendStarlight_NotEnough_IsRejected_WithoutWriting()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await SeedStarlightAsync(db, 3, 4);
            LedgerException ex = await Assert.ThrowsAsync<LedgerException>(() => SpendAsync(db, 8));
            Assert.Equal(ErrorCode.BadRequest, ex.Code);   // [임시] ERR_INSUFFICIENT 에 대응하는 별빛 전용 코드가 ErrorCode 에 없다
            Assert.Equal(3L, await BalanceAsync(db, "starlight", "free"));
            Assert.Equal(4L, await BalanceAsync(db, "starlight", "paid"));
        }

        [Fact]
        public async Task SpendStarlight_NegativePaidAfterRefund_IsBlocked_EvenWithFreeLeft()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await SeedStarlightAsync(db, 100, -5);
            LedgerException ex = await Assert.ThrowsAsync<LedgerException>(() => SpendAsync(db, 1));
            Assert.Equal(ErrorCode.PaymentBlocked, ex.Code);   // ERR_STARLIGHT_BLOCKED [임시 대응]
            Assert.Equal(100L, await BalanceAsync(db, "starlight", "free"));
        }

        [Fact]
        public async Task SpendStarlight_NonPositive_IsAProgrammerError()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.Ledger.SpendStarlightAsync(uow, Acct, 0, Ref()));
        }

        [Fact]
        public async Task SpendStarlight_Concurrent_NeverOverspends()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await SeedStarlightAsync(db, 5, 5);
            Task<bool>[] spends = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                try
                {
                    await SpendAsync(db, 3);
                    return true;
                }
                catch (LedgerException)
                {
                    return false;
                }
            })).ToArray();
            bool[] ok = await Task.WhenAll(spends);
            Assert.Equal(3, ok.Count(x => x));   // 10 / 3 = 3번
            Assert.Equal(1L, await BalanceAsync(db, "starlight", "free") + await BalanceAsync(db, "starlight", "paid"));
            Assert.Equal(0L, await BalanceAsync(db, "starlight", "free"));
        }

        // ---- 무작위 요청 뒤 대조(server-engineer 완료 정의) ----

        [Fact]
        public async Task RandomOperations_LedgerSumsEqualBalances()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long[] accounts = { 3, 7, 11 };
            string[] codes = { "mat.stone_low", "mat.charm" };
            var rng = new Random(20261112);   // 시험 입력만 만든다(서버 코드는 난수를 쓰지 않는다)
            int rejected = 0;
            for (int i = 0; i < 300; i++)
            {
                await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
                long a = accounts[rng.Next(accounts.Length)];
                bool pending = rng.Next(4) == 0;
                try
                {
                    switch (rng.Next(4))
                    {
                        case 0:
                            await db.Ledger.AddCurrencyAsync(uow, a, CurrencyCode.Gold, CurrencyBucket.Earned, rng.Next(1, 200) * (rng.Next(3) == 0 ? -1 : 1), pending, Ref());
                            break;
                        case 1:
                            await db.Ledger.AddCurrencyAsync(uow, a, CurrencyCode.Starlight, rng.Next(2) == 0 ? CurrencyBucket.Free : CurrencyBucket.Paid,
                                rng.Next(1, 30), false, Ref());
                            break;
                        case 2:
                            await db.Ledger.SpendStarlightAsync(uow, a, rng.Next(1, 40), Ref());
                            break;
                        default:
                            await db.Ledger.AddItemAsync(uow, a, ItemRefs.Stack(codes[rng.Next(codes.Length)]), rng.Next(1, 5) * (rng.Next(3) == 0 ? -1 : 1), pending, Ref());
                            break;
                    }
                    await uow.CommitAsync(CancellationToken.None);
                }
                catch (Exception e) when (e is PostgresException || e is LedgerException)
                {
                    rejected++;   // CHECK 위반과 별빛 부족은 트랜잭션째 버려진다
                }
            }
            Assert.InRange(rejected, 1, 299);   // 거절과 성공이 둘 다 일어났다

            List<(long, string, string, long, long)> currencyMismatch = await db.QueryAsync<(long, string, string, long, long)>(@"
SELECT b.account_id, b.currency, b.bucket, b.amount, b.amount_pending FROM currency_balance b
LEFT JOIN (SELECT account_id, currency, bucket,
                  COALESCE(sum(delta) FILTER (WHERE NOT pending), 0) AS settled,
                  COALESCE(sum(delta) FILTER (WHERE pending), 0) AS pend
           FROM currency_ledger GROUP BY account_id, currency, bucket) l
  USING (account_id, currency, bucket)
WHERE b.amount <> COALESCE(l.settled, 0) OR b.amount_pending <> COALESCE(l.pend, 0)");
            Assert.Empty(currencyMismatch);
            List<(long, string)> stackMismatch = await db.QueryAsync<(long, string)>(@"
SELECT s.account_id, s.item_code FROM item_stack s
LEFT JOIN (SELECT account_id, substr(item_ref, 7) AS item_code,
                  COALESCE(sum(delta) FILTER (WHERE NOT pending), 0) AS settled,
                  COALESCE(sum(delta) FILTER (WHERE pending), 0) AS pend
           FROM item_ledger WHERE item_ref LIKE 'stack:%' GROUP BY account_id, item_ref) l
  USING (account_id, item_code)
WHERE s.qty <> COALESCE(l.settled, 0) OR s.qty_pending <> COALESCE(l.pend, 0)");
            Assert.Empty(stackMismatch);
            // 잔액 행 없이 원장 줄만 있는 구분도 없다
            Assert.Equal(0L, await db.ScalarAsync<long>(
                "SELECT count(*) FROM currency_ledger l WHERE NOT EXISTS (SELECT 1 FROM currency_balance b " +
                "WHERE b.account_id = l.account_id AND b.currency = l.currency AND b.bucket = l.bucket)"));
        }
    }
}
