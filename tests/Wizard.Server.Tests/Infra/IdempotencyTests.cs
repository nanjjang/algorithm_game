using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Wizard.Content;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 3·5, CONTRACTS 7.2 IIdempotency
    public sealed class IdempotencyTests
    {
        private const long Acct = 7;

        private static async Task<PgTestDatabase> SetupAsync()
        {
            PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await Probes.AddAccountsAsync(db, Acct);
            return db;
        }

        private static byte[] Fp(string body) => Probes.Fingerprint("k", body);

        [Fact]
        public async Task DifferentBodySameKey_IsConflict_AndHasNoSideEffect()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            int runs = 0;
            IdempotentResult<int> first = await db.Idempotency.RunAsync(Acct, r, "k", Fp("{\"n\":1}"), async uow =>
            {
                runs++;
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 10, false, Ref(r));
                return 1;
            }, CancellationToken.None);
            IdempotentResult<int> second = await db.Idempotency.RunAsync(Acct, r, "k", Fp("{\"n\":2}"), async uow =>
            {
                runs++;
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 10, false, Ref(r));
                return 2;
            }, CancellationToken.None);

            Assert.False(first.Conflict);
            Assert.True(second.Conflict);
            Assert.False(second.Replayed);
            Assert.Equal(1, runs);
            Assert.Equal(10L, await Probes.GoldAsync(db, Acct));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
        }

        [Fact]
        public async Task DifferentKindSameKeyAndFingerprint_IsConflict()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            byte[] fp = Fp("{}");
            await db.Idempotency.RunAsync(Acct, r, "a", fp, _ => Task.FromResult(1), CancellationToken.None);
            IdempotentResult<int> other = await db.Idempotency.RunAsync(Acct, r, "b", fp, _ => Task.FromResult(2), CancellationToken.None);
            Assert.True(other.Conflict);
        }

        [Fact]
        public async Task SameRequestIdOnAnotherAccount_IsIndependent()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            IdempotentResult<int> a = await db.Idempotency.RunAsync(1, r, "k", Fp("{}"), _ => Task.FromResult(1), CancellationToken.None);
            IdempotentResult<int> b = await db.Idempotency.RunAsync(2, r, "k", Fp("{}"), _ => Task.FromResult(2), CancellationToken.None);
            Assert.False(a.Replayed);
            Assert.False(b.Replayed);
            Assert.Equal(2, b.Value);
        }

        [Fact]
        public async Task ConcurrentDifferentBodiesSameKey_OneRuns_TheOtherConflicts()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            int runs = 0;
            var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<IdempotentResult<int>> first = Task.Run(() => db.Idempotency.RunAsync(Acct, r, "k", Fp("A"), async _ =>
            {
                Interlocked.Increment(ref runs);
                inside.TrySetResult();
                await Task.Delay(300);
                return 1;
            }, CancellationToken.None));
            await inside.Task;
            IdempotentResult<int> second = await db.Idempotency.RunAsync(Acct, r, "k", Fp("B"), _ =>
            {
                Interlocked.Increment(ref runs);
                return Task.FromResult(2);
            }, CancellationToken.None);
            Assert.True(second.Conflict);
            Assert.False((await first).Conflict);
            Assert.Equal(1, runs);
        }

        [Fact]
        public async Task ManyConcurrentSameRequests_BodyRunsExactlyOnce()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            int runs = 0;
            using var start = new ManualResetEventSlim(false);
            Task<IdempotentResult<long>>[] all = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return db.Idempotency.RunAsync(Acct, r, "k", Fp("{}"), async uow =>
                {
                    Interlocked.Increment(ref runs);
                    await Task.Delay(100);
                    return await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 25, false, Ref(r));
                }, CancellationToken.None);
            })).ToArray();
            start.Set();
            IdempotentResult<long>[] results = await Task.WhenAll(all);
            Assert.Equal(1, runs);
            Assert.Equal(1, results.Count(x => !x.Replayed));
            Assert.All(results, x => Assert.Equal(25L, x.Value));
            Assert.Equal(25L, await Probes.GoldAsync(db, Acct));
        }

        [Fact]
        public async Task RuleRejectionResult_IsStoredAndReplayed()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            int runs = 0;
            Func<IUnitOfWork, Task<ProbeEnhanceResult>> rejecting = _ =>
            {
                runs++;
                return Task.FromResult(new ProbeEnhanceResult { Outcome = "not_enough_gold" });
            };
            await db.Idempotency.RunAsync(Acct, r, "k", Fp("{}"), rejecting, CancellationToken.None);
            IdempotentResult<ProbeEnhanceResult> again = await db.Idempotency.RunAsync(Acct, r, "k", Fp("{}"), rejecting, CancellationToken.None);
            Assert.True(again.Replayed);
            Assert.Equal("not_enough_gold", again.Value.Outcome);
            Assert.Equal(1, runs);
        }

        [Fact]
        public async Task BodyThrows_RollsBackTheRequestRowToo()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.Idempotency.RunAsync<int>(Acct, r, "k", Fp("{}"), async uow =>
            {
                await db.Ledger.AddCurrencyAsync(uow, Acct, CurrencyCode.Gold, CurrencyBucket.Earned, 10, false, Ref(r));
                throw new InvalidOperationException("boom");
            }, CancellationToken.None));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM request"));
            Assert.Equal(0L, await Probes.GoldAsync(db, Acct));
        }

        [Fact]
        public async Task BodyMustNotCommit()
        {
            await using PgTestDatabase db = await SetupAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.Idempotency.RunAsync(Acct, Guid.NewGuid(), "k", Fp("{}"), async uow =>
            {
                await uow.CommitAsync(CancellationToken.None);
                return 1;
            }, CancellationToken.None));
            Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM request"));
        }

        [Fact]
        public async Task StoredResult_IsJsonInTheRequestRow_WithFingerprintAndServerTime()
        {
            await using PgTestDatabase db = await SetupAsync();
            Guid r = Guid.NewGuid();
            byte[] fp = Fp("{\"x\":1}");
            await db.Idempotency.RunAsync(Acct, r, "k", fp, _ => Task.FromResult(new ProbeEnhanceResult { Roll = 42, GoldAfter = 9, Outcome = "success" }),
                CancellationToken.None);
            var row = (await db.QueryAsync<(string Kind, byte[] Fingerprint, string Result, DateTime At)>(
                "SELECT kind, fingerprint, result::text, at FROM request WHERE request_id = @R", new { R = r })).Single();
            Assert.Equal("k", row.Kind);
            Assert.Equal(fp, row.Fingerprint);
            Assert.Contains("\"roll\": 42", row.Result, StringComparison.Ordinal);
            Assert.Equal(db.Clock.UtcNow, row.At.ToUniversalTime());
        }

        [Fact]
        public async Task EmptyFingerprintOrKind_IsAProgrammerError()
        {
            await using PgTestDatabase db = await SetupAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => db.Idempotency.RunAsync(Acct, Guid.NewGuid(), "k", ReadOnlyMemory<byte>.Empty,
                _ => Task.FromResult(1), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => db.Idempotency.RunAsync(Acct, Guid.NewGuid(), "", Fp("{}"),
                _ => Task.FromResult(1), CancellationToken.None));
        }

        private static LedgerRef Ref(Guid r) => new LedgerRef(LedgerReason.Admin, "test", "x", r);
    }
}
