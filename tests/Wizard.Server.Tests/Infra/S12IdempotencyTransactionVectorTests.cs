using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Wizard.Content;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // spec 12 14.11절 T-IDEM-01~12 (멱등·트랜잭션). 기대값은 명세 표 그대로다.
    // 강화·우편·장터 벡터(01~04, 07, 08)는 이 작업의 기반 위에 그 유스케이스의 트랜잭션 모양(Probes)을 올려 돌린다.
    // 진짜 유스케이스(M10-ENH-01, M13-MAIL-01, M13-MKT-01)가 들어오면 그 작업이 같은 벡터를 자기 코드로 다시 돌린다.
    public sealed class S12IdempotencyTransactionVectorTests
    {
        private const long Acct = 7;
        private const string EnhanceKind = "wand.enhance";

        private static async Task<PgTestDatabase> SetupAsync(long gold = 1000, long stones = 5)
        {
            PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await Probes.CreateAsync(db);
            await Probes.AddAccountsAsync(db, Acct);
            await Probes.GrantAsync(db, Acct, gold, stones);
            return db;
        }

        private static Task<IdempotentResult<ProbeEnhanceResult>> EnhanceOnceAsync(PgTestDatabase db, Guid r, Func<int> nextRoll,
            Func<IUnitOfWork, Task>? afterWork = null)
        {
            byte[] fp = Probes.Fingerprint(EnhanceKind, "{\"wand_id\":11,\"use_charm\":false}");
            return db.Idempotency.RunAsync(Acct, r, EnhanceKind, fp, async uow =>
            {
                ProbeEnhanceResult res = await Probes.EnhanceAsync(db, uow, Acct, r, nextRoll());
                if (afterWork != null)
                {
                    await afterWork(uow);
                }
                return res;
            }, CancellationToken.None);
        }

        [Fact]
        public async Task S12_T_IDEM_01__SameRequestAgain_ReturnsFirstResult_OneLog_OneDebit()
        {
            await using PgTestDatabase db = await SetupAsync();
            int rolls = 0;
            Guid r = Guid.NewGuid();
            IdempotentResult<ProbeEnhanceResult> first = await EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls));
            IdempotentResult<ProbeEnhanceResult> second = await EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls));

            Assert.False(first.Replayed);
            Assert.True(second.Replayed);
            Assert.False(second.Conflict);
            Assert.Equal(first.Value.Roll, second.Value.Roll);
            Assert.Equal(first.Value.GoldAfter, second.Value.GoldAfter);
            Assert.Equal(first.Value.Outcome, second.Value.Outcome);
            Assert.Equal(1, rolls);                                                              // 다시 굴리지 않았다
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_enhance_log"));   // enhance_log 1행
            Assert.Equal(1000 - Probes.EnhanceGoldCost, await Probes.GoldAsync(db, Acct));       // 재료 차감 1회
            Assert.Equal(4L, await Probes.StonesAsync(db, Acct));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger WHERE reason = 'enhance'"));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM item_ledger WHERE reason = 'enhance'"));
        }

        [Fact]
        public async Task S12_T_IDEM_02__SameRequestTwiceConcurrently_RunsOnce_BothGetTheSameResult()
        {
            await using PgTestDatabase db = await SetupAsync();
            int rolls = 0;
            Guid r = Guid.NewGuid();
            var firstInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // 첫 요청이 트랜잭션 안(잠금과 차감 뒤, 커밋 전)에 있을 때 두 번째가 들어온다
            Task<IdempotentResult<ProbeEnhanceResult>> a = Task.Run(() => EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls), async _ =>
            {
                firstInside.TrySetResult();
                await Task.Delay(400);
            }));
            await firstInside.Task;
            Task<IdempotentResult<ProbeEnhanceResult>> b = Task.Run(() => EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls)));
            IdempotentResult<ProbeEnhanceResult>[] both = await Task.WhenAll(a, b);

            Assert.Equal(1, rolls);
            Assert.Equal(1, both.Count(x => !x.Replayed));
            Assert.Equal(1, both.Count(x => x.Replayed));
            Assert.All(both, x => Assert.False(x.Conflict));
            Assert.Equal(both[0].Value.Roll, both[1].Value.Roll);
            Assert.Equal(both[0].Value.GoldAfter, both[1].Value.GoldAfter);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_enhance_log"));
            Assert.Equal(1000 - Probes.EnhanceGoldCost, await Probes.GoldAsync(db, Acct));
        }

        [Fact]
        public async Task S12_T_IDEM_03__ServerKilledBeforeCommit_SameRequestRunsAnew_MaterialsWereUntouched()
        {
            await using PgTestDatabase db = await SetupAsync();
            int rolls = 0;
            Guid r = Guid.NewGuid();
            // 차감까지 한 뒤 커밋 전에 그 트랜잭션의 서버 연결을 강제로 끊는다
            await Assert.ThrowsAnyAsync<NpgsqlException>(() => EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls), async uow =>
            {
                await PgTestDatabase.TerminateBackendAsync(uow.Connection.ProcessID);
            }));

            Assert.Equal(1000L, await Probes.GoldAsync(db, Acct));                                // 재료 그대로였음
            Assert.Equal(5L, await Probes.StonesAsync(db, Acct));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_enhance_log"));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM request"));

            IdempotentResult<ProbeEnhanceResult> again = await EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls));
            Assert.False(again.Replayed);                                                         // 새로 실행됨
            Assert.Equal(2, rolls);
            Assert.Equal(1000 - Probes.EnhanceGoldCost, await Probes.GoldAsync(db, Acct));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_enhance_log"));
        }

        [Fact]
        public async Task S12_T_IDEM_04__CommittedButResponseLost_SameRequestGetsTheStoredResult()
        {
            await using PgTestDatabase db = await SetupAsync();
            int rolls = 0;
            Guid r = Guid.NewGuid();
            ProbeEnhanceResult committed = (await EnhanceOnceAsync(db, r, () => Interlocked.Increment(ref rolls))).Value;
            // (응답이 클라이언트에 닿지 못했다) 재시작한 서버에서 같은 R. 새 서버 프로세스 = 새 연결 풀
            await using NpgsqlDataSource restarted = NpgsqlDataSource.Create(db.ConnectionString);
            var idem = new PgIdempotency(new PgUnitOfWorkFactory(restarted, DbRoles.App), db.Clock);
            byte[] fp = Probes.Fingerprint(EnhanceKind, "{\"wand_id\":11,\"use_charm\":false}");
            IdempotentResult<ProbeEnhanceResult> recovered = await idem.RunAsync(Acct, r, EnhanceKind, fp,
                uow => Probes.EnhanceAsync(db, uow, Acct, r, Interlocked.Increment(ref rolls)), CancellationToken.None);

            Assert.True(recovered.Replayed);   // 클라이언트는 이 표시로 결과 연출을 다시 튼다(enhance.result_recovered, 화면 쪽)
            Assert.Equal(committed.Roll, recovered.Value.Roll);
            Assert.Equal(committed.GoldAfter, recovered.Value.GoldAfter);
            Assert.Equal(1, rolls);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_enhance_log"));
            Assert.Equal(1000 - Probes.EnhanceGoldCost, await Probes.GoldAsync(db, Acct));
        }

        [Fact]
        public async Task S12_T_IDEM_05__RequestRow31DaysOld_IsDeletedByCleanup()
        {
            await using PgTestDatabase db = await SetupAsync();
            DateTime now = db.Clock.UtcNow;
            Guid old = Guid.NewGuid();
            Guid recent = Guid.NewGuid();
            byte[] fp = Probes.Fingerprint("noop", "{}");
            db.Clock.Set(now - TimeSpan.FromDays(31));
            await db.Idempotency.RunAsync(Acct, old, "noop", fp, _ => Task.FromResult(1), CancellationToken.None);
            db.Clock.Set(now - TimeSpan.FromDays(29));
            await db.Idempotency.RunAsync(Acct, recent, "noop", fp, _ => Task.FromResult(2), CancellationToken.None);
            db.Clock.Set(now);

            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                int deleted = await db.Idempotency.PurgeOlderThanAsync(uow, now - TimeSpan.FromDays(30), CancellationToken.None);   // 30일 보관(8.4절)
                await uow.CommitAsync(CancellationToken.None);
                Assert.Equal(1, deleted);
            }
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM request WHERE request_id = @R", new { R = old }));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM request WHERE request_id = @R", new { R = recent }));
        }

        [Fact]
        public async Task S12_T_IDEM_06__DirectTradeBetween7And3_LocksAccount3Then7()
        {
            await using PgTestDatabase db = await SetupAsync();
            await Probes.AddAccountsAsync(db, 3);
            // 다른 세션이 7 을 쥐고 있다. 거래 트랜잭션이 (7, 3)을 잠그면 오름차순이라 3 을 먼저 잡고 7 에서 기다린다
            await using IUnitOfWork holder = await db.App.BeginAsync(CancellationToken.None);
            await db.Locks.LockAccountsAsync(holder, 7);

            await using IUnitOfWork trade = await db.App.BeginAsync(CancellationToken.None);
            int tradePid = trade.Connection.ProcessID;
            Task locking = db.Locks.LockAccountsAsync(trade, 7, 3);
            await WaitUntilBlockedAsync(db, tradePid);

            // 3 은 이미 거래 트랜잭션이 쥐었다(내림차순이었다면 7 에서 먼저 막혀 3 은 비어 있다)
            await using (IUnitOfWork probe = await db.App.BeginAsync(CancellationToken.None))
            {
                PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => probe.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT id FROM account WHERE id = 3 FOR UPDATE NOWAIT", transaction: probe.Transaction)));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, ex.SqlState);
            }
            Assert.False(locking.IsCompleted);
            await holder.DisposeAsync();     // 7 을 놓으면 거래가 7 까지 잡는다
            await locking;
        }

        [Fact]
        public async Task S12_T_IDEM_07__UnclaimedMail_TwoConcurrentClaims_PayOnce()
        {
            await using PgTestDatabase db = await SetupAsync(gold: 0, stones: 0);
            const int mails = 10;
            for (int i = 1; i <= mails; i++)
            {
                await db.ExecAsync("INSERT INTO probe_mail (id, account_id, gold) VALUES (@Id, @A, 50)", new { Id = i, A = Acct });
            }
            for (int i = 1; i <= mails; i++)
            {
                long mailId = i;
                // 두 기기가 각자의 요청 번호로 같은 우편을 동시에 받는다
                Task<bool>[] claims = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
                {
                    await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
                    bool paid = await Probes.ClaimMailAsync(db, uow, mailId);
                    await uow.CommitAsync(CancellationToken.None);
                    return paid;
                })).ToArray();
                bool[] results = await Task.WhenAll(claims);
                Assert.Equal(1, results.Count(x => x));
            }
            Assert.Equal(50L * mails, await Probes.GoldAsync(db, Acct));
            Assert.Equal((long)mails, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger WHERE reason = 'mail'"));
        }

        [Fact]
        public async Task S12_T_IDEM_08__LastListingItem_TwoConcurrentBuyers_OnlyOneSucceeds()
        {
            await using PgTestDatabase db = await SetupAsync(gold: 0, stones: 0);
            const long seller = 20;
            long[] buyers = { 30, 10 };   // 하나는 판매자보다 작은 번호, 하나는 큰 번호
            await Probes.AddAccountsAsync(db, seller, buyers[0], buyers[1]);
            foreach (long b in buyers)
            {
                await Probes.GrantAsync(db, b, 500, 0);
            }
            const int rounds = 10;
            for (int i = 1; i <= rounds; i++)
            {
                await db.ExecAsync("INSERT INTO probe_listing (id, seller_id, qty, unit_price) VALUES (@Id, @S, 1, 40)", new { Id = i, S = seller });
                long listingId = i;
                Task<string>[] buys = buyers.Select(b => Task.Run(async () =>
                {
                    await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
                    string outcome = await Probes.BuyAsync(db, uow, b, listingId);
                    await uow.CommitAsync(CancellationToken.None);
                    return outcome;
                })).ToArray();
                string[] outcomes = await Task.WhenAll(buys);
                Assert.Equal(1, outcomes.Count(o => o == "ok"));
                Assert.Equal(1, outcomes.Count(o => o == "listing_gone"));
            }
            Assert.Equal(40L * rounds, await Probes.GoldAsync(db, seller));
            Assert.Equal(1000L - 40L * rounds, await Probes.GoldAsync(db, buyers[0]) + await Probes.GoldAsync(db, buyers[1]));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM probe_listing WHERE qty <> 0"));
        }

        [Theory]
        [InlineData("UPDATE currency_ledger SET delta = delta + 1")]
        [InlineData("DELETE FROM currency_ledger")]
        public async Task S12_T_IDEM_09__ServerRole_CannotUpdateOrDeleteTheLedger(string sql)
        {
            await using PgTestDatabase db = await SetupAsync();
            long before = await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger");
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => uow.Connection.ExecuteAsync(new CommandDefinition(sql, transaction: uow.Transaction)));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);   // 권한 거부
                Assert.Contains("permission denied", ex.MessageText, StringComparison.Ordinal);
            }
            Assert.Equal(before, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
        }

        [Fact]
        public async Task S12_T_IDEM_10__StackQtyZero_MinusOne_IsCheckViolation_AndTheTransactionIsCancelled()
        {
            await using PgTestDatabase db = await SetupAsync(gold: 0, stones: 0);
            await db.ExecAsync("INSERT INTO item_stack (account_id, item_code, qty) VALUES (@A, @C, 0)", new { A = Acct, C = Probes.Stone });
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 5, false, Ref());   // 같은 트랜잭션의 다른 변경
                PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                    db.Ledger.AddItemAsync(uow, Acct, ItemRefs.Stack(Probes.Stone), -1, false, Ref()));
                Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
                await Assert.ThrowsAnyAsync<NpgsqlException>(() => uow.CommitAsync(CancellationToken.None));   // 취소된 트랜잭션은 커밋되지 않는다
            }
            Assert.Equal(0L, await Probes.StonesAsync(db, Acct));
            Assert.Equal(0L, await Probes.GoldAsync(db, Acct));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM item_ledger"));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
        }

        [Fact]
        public async Task S12_T_IDEM_11__GoldEarnedZero_MinusOne_IsCheckViolation()
        {
            await using PgTestDatabase db = await SetupAsync(gold: 0, stones: 0);
            await db.ExecAsync("INSERT INTO currency_balance (account_id, currency, bucket, amount) VALUES (@A, 'gold', 'earned', 0)", new { A = Acct });
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
                    db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, -1, false, Ref()));
                Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
            }
            Assert.Equal(0L, await Probes.GoldAsync(db, Acct));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
        }

        [Fact]
        public async Task S12_T_IDEM_12__StarlightPaidZero_MinusFiveRefund_IsAllowed()
        {
            await using PgTestDatabase db = await SetupAsync(gold: 0, stones: 0);
            await db.ExecAsync("INSERT INTO currency_balance (account_id, currency, bucket, amount) VALUES (@A, 'starlight', 'paid', 0)", new { A = Acct });
            long after;
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                after = await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Starlight, CurrencyBucket.Paid, -5, false,
                    new LedgerRef(LedgerReason.Refund, "order", Guid.NewGuid().ToString("D"), null));
                await uow.CommitAsync(CancellationToken.None);
            }
            Assert.Equal(-5L, after);
            Assert.Equal(-5L, await db.ScalarAsync<long>(
                "SELECT amount FROM currency_balance WHERE account_id = @A AND currency = 'starlight' AND bucket = 'paid'", new { A = Acct }));
            Assert.Equal(-5L, await db.ScalarAsync<long>("SELECT balance_after FROM currency_ledger WHERE reason = 'refund'"));
        }

        private static LedgerRef Ref() => new LedgerRef(LedgerReason.Admin, "test", "x", null);

        // 그 세션이 잠금을 기다리는 상태가 될 때까지
        private static async Task WaitUntilBlockedAsync(PgTestDatabase db, int pid)
        {
            DateTime until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < until)
            {
                string? wait = await db.ScalarAsync<string?>("SELECT wait_event_type FROM pg_stat_activity WHERE pid = @P", new { P = pid });
                if (wait == "Lock")
                {
                    return;
                }
                await Task.Delay(20);
            }
            throw new TimeoutException("session " + pid + " never blocked on a lock");
        }
    }
}
