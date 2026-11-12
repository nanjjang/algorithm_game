using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // PgMailOutboxStore(2.8절 대기열 상태 기계), PgAccountSettingsStore(설정 동기화), PgSanctionStore(제재)
    public sealed class OutboxSettingsSanctionStoreTests
    {
        // 2.8절: Queued → (실패) Failed(next_try_at) → … → Sent | Dropped. 끝난 줄은 다시 바뀌지 않는다
        [Fact]
        public async Task Outbox_StateMachine()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var outbox = new PgMailOutboxStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long m = await outbox.EnqueueAsync(uow, MailOutboxKind.ResetPassword, 7, 11, t0);
            MailOutboxRow q = (await outbox.GetAsync(uow, m))!;
            Assert.Equal(MailOutboxStatus.Queued, q.MailStatus);
            Assert.Equal(MailOutboxKind.ResetPassword, q.MailKind);
            Assert.Equal(7L, q.AccountId);
            Assert.Equal(11L, q.TokenId);
            Assert.Equal((short)0, q.Tries);
            Assert.Equal(t0, q.NextTryAt);

            Assert.True(await outbox.MarkRetryAsync(uow, m, t0.AddMinutes(1)));
            MailOutboxRow f = (await outbox.GetAsync(uow, m))!;
            Assert.Equal(MailOutboxStatus.Failed, f.MailStatus);
            Assert.Equal((short)1, f.Tries);
            Assert.Equal(t0.AddMinutes(1), f.NextTryAt);

            Assert.True(await outbox.MarkSentAsync(uow, m, "prov-1", t0.AddMinutes(1)));
            MailOutboxRow s = (await outbox.GetAsync(uow, m))!;
            Assert.Equal(MailOutboxStatus.Sent, s.MailStatus);
            Assert.Equal((short)2, s.Tries);
            Assert.Equal("prov-1", s.ProviderRef);
            Assert.Equal(t0.AddMinutes(1), s.SentAt);
            Assert.Null(s.NextTryAt);
            Assert.False(await outbox.MarkRetryAsync(uow, m, t0));
            Assert.False(await outbox.MarkDroppedAsync(uow, m, true));
            Assert.False(await outbox.MarkSentAsync(uow, m, "again", t0));

            long expired = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 7, 12, t0);
            Assert.True(await outbox.MarkDroppedAsync(uow, expired, false));   // 코드가 만료돼 보내지 않고 버림: 시도 수는 그대로
            MailOutboxRow d = (await outbox.GetAsync(uow, expired))!;
            Assert.Equal(MailOutboxStatus.Dropped, d.MailStatus);
            Assert.Equal((short)0, d.Tries);
            long gaveUp = await outbox.EnqueueAsync(uow, MailOutboxKind.NotifyPasswordChanged, null, null, t0);
            Assert.True(await outbox.MarkDroppedAsync(uow, gaveUp, true));
            Assert.Equal((short)1, (await outbox.GetAsync(uow, gaveUp))!.Tries);
        }

        // DECISIONS-TECH T-56: 상태는 spec 12 8.1절의 넷뿐이다. 반송 사건은 Sent → Dropped 이고 발송 시각과 참조 값은 남는다.
        // 같은 사건이 두 번 와도 한 번만 바뀐다. 보내지 못한 줄(Queued, Failed, 버린 줄)은 반송이 될 수 없다
        [Fact]
        public async Task Outbox_BounceClosesASentRow_Once()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var outbox = new PgMailOutboxStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long sent = await outbox.EnqueueAsync(uow, MailOutboxKind.ResetPassword, 7, 11, t0);
            long other = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 8, null, t0);
            long queued = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 9, null, t0);
            long gaveUp = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 9, null, t0);
            Assert.True(await outbox.MarkSentAsync(uow, sent, "prov-7", t0.AddMinutes(1)));
            Assert.True(await outbox.MarkSentAsync(uow, other, "prov-8", t0.AddMinutes(2)));
            Assert.True(await outbox.MarkDroppedAsync(uow, gaveUp, true));
            Assert.False((await outbox.GetAsync(uow, sent))!.Bounced);
            Assert.False((await outbox.GetAsync(uow, gaveUp))!.Bounced);   // 보내지 못하고 버린 줄은 반송이 아니다
            Assert.Null(await outbox.LastBouncedSentAtAsync(uow, 7));
            Assert.Null(await outbox.LastBouncedSentAtAsync(uow, 9));

            MailOutboxRow? b = await outbox.MarkBouncedAsync(uow, "prov-7");
            Assert.NotNull(b);
            Assert.Equal(sent, b!.Id);
            Assert.Equal(7L, b.AccountId);
            Assert.Equal(MailOutboxStatus.Dropped, b.MailStatus);
            Assert.True(b.Bounced);
            Assert.Equal(t0.AddMinutes(1), b.SentAt);
            Assert.Equal("prov-7", b.ProviderRef);
            Assert.Equal((short)1, b.Tries);
            Assert.Null(await outbox.MarkBouncedAsync(uow, "prov-7"));     // 같은 사건 두 번
            Assert.Null(await outbox.MarkBouncedAsync(uow, "prov-none"));  // 모르는 참조 값
            Assert.Equal(MailOutboxStatus.Sent, (await outbox.GetAsync(uow, other))!.MailStatus);
            Assert.Equal(MailOutboxStatus.Queued, (await outbox.GetAsync(uow, queued))!.MailStatus);
            Assert.Equal(t0.AddMinutes(1), await outbox.LastBouncedSentAtAsync(uow, 7));
            Assert.Null(await outbox.LastBouncedSentAtAsync(uow, 8));
            // 닫힌 줄은 다시 바뀌지 않는다
            Assert.False(await outbox.MarkSentAsync(uow, sent, "again", t0));
            Assert.False(await outbox.MarkRetryAsync(uow, sent, t0));
            Assert.False(await outbox.MarkDroppedAsync(uow, sent, true));
            await Assert.ThrowsAsync<ArgumentException>(() => outbox.MarkBouncedAsync(uow, ""));
            // 반송은 하루 발송 수를 돌려주지 않는다(사업자는 그 메일을 받았다). 실패율에는 dropped 로 잡힌다
            Assert.Equal(2, await outbox.CountSentSinceAsync(uow, t0));
            MailOutboxCounts counts = await outbox.CountsSinceAsync(uow, t0);
            Assert.Equal(1L, counts.Sent);
            Assert.Equal(2L, counts.Dropped);
            Assert.Equal(1L, counts.Queued);
        }

        // 같은 참조 값의 Sent 줄이 둘이어도(사업자의 잘못) 사건 하나는 한 줄만 닫는다
        [Fact]
        public async Task Outbox_Bounce_WithDuplicateProviderRef_ClosesOneRowPerEvent()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var outbox = new PgMailOutboxStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long first = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 1, null, t0);
            long second = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 2, null, t0);
            Assert.True(await outbox.MarkSentAsync(uow, first, "dup", t0));
            Assert.True(await outbox.MarkSentAsync(uow, second, "dup", t0));
            Assert.Equal(first, (await outbox.MarkBouncedAsync(uow, "dup"))!.Id);
            Assert.Equal(second, (await outbox.MarkBouncedAsync(uow, "dup"))!.Id);
            Assert.Null(await outbox.MarkBouncedAsync(uow, "dup"));
        }

        // 꺼낼 때가 된 줄만(queued, failed), next_try_at 순. 발송 작업 둘이 동시에 꺼내면 서로 다른 줄을 받는다(SKIP LOCKED)
        [Fact]
        public async Task Outbox_LockDue_SkipsLockedRows_AndRowsNotYetDue()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var outbox = new PgMailOutboxStore();
            DateTime t0 = db.Clock.UtcNow;
            var ids = new List<long>();
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                for (int i = 0; i < 4; i++)
                {
                    ids.Add(await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, i, null, t0.AddSeconds(i)));
                }
                Assert.True(await outbox.MarkRetryAsync(uow, ids[0], t0.AddMinutes(5)));   // 아직 때가 아니다
                long sent = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 9, null, t0);
                Assert.True(await outbox.MarkSentAsync(uow, sent, null, t0));
                await uow.CommitAsync(AccountsKit.None);
            }
            DateTime now = t0.AddMinutes(1);
            await using IUnitOfWork w1 = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork w2 = await db.App.BeginAsync(AccountsKit.None);
            IReadOnlyList<MailOutboxRow> first = await outbox.LockDueAsync(w1, now, 2);
            IReadOnlyList<MailOutboxRow> second = await outbox.LockDueAsync(w2, now, 10).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { ids[1], ids[2] }, first.Select(r => r.Id).ToArray());
            Assert.Equal(new[] { ids[3] }, second.Select(r => r.Id).ToArray());
            // 5분 뒤: 다시 시도할 때가 된 ids[0] 이 들어오고, w2 가 쥔 ids[3] 은 건너뛴다(자기가 쥔 줄은 다시 받는다)
            Assert.Equal(new[] { ids[1], ids[2], ids[0] }, (await outbox.LockDueAsync(w1, t0.AddMinutes(5), 10)).Select(r => r.Id).ToArray());
        }

        // 하루 발송 상한(2.8절)과 실패율 감시의 바탕, 30일 정리
        [Fact]
        public async Task Outbox_CountsAndCleanup()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var outbox = new PgMailOutboxStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long a = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 1, null, t0);
            long b = await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 1, null, t0);
            long c = await outbox.EnqueueAsync(uow, MailOutboxKind.FindLoginIds, 1, null, t0.AddHours(2));
            long d = await outbox.EnqueueAsync(uow, MailOutboxKind.NotifyEmailChanged, 1, null, t0.AddHours(2));
            await outbox.EnqueueAsync(uow, MailOutboxKind.VerifyEmail, 1, null, t0.AddHours(2));
            Assert.True(await outbox.MarkSentAsync(uow, a, null, t0));
            Assert.True(await outbox.MarkSentAsync(uow, b, null, t0.AddHours(2)));
            Assert.True(await outbox.MarkRetryAsync(uow, c, t0.AddHours(3)));
            Assert.True(await outbox.MarkDroppedAsync(uow, d, true));
            Assert.Equal(2, await outbox.CountSentSinceAsync(uow, t0));
            Assert.Equal(1, await outbox.CountSentSinceAsync(uow, t0.AddHours(1)));
            MailOutboxCounts recent = await outbox.CountsSinceAsync(uow, t0.AddHours(1));
            Assert.Equal(1L, recent.Queued);
            Assert.Equal(0L, recent.Sent);   // b 는 t0 에 만들어졌다(만든 시각으로 센다)
            Assert.Equal(1L, recent.Failed);
            Assert.Equal(1L, recent.Dropped);
            Assert.Equal(3L, recent.Total);
            Assert.Equal(5L, (await outbox.CountsSinceAsync(uow, t0)).Total);
            Assert.Equal(2, await outbox.DeleteBeforeAsync(uow, t0.AddHours(1)));
        }

        [Fact]
        public async Task Settings_PutReplacesTheWholeDocument_WithServerTime()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long a = await AccountsKit.CreateAsync(db, new PgAccountStore(AccountsKit.NewProtector()), "user0001", "Nick1", "x@y.co");
            var settings = new PgAccountSettingsStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            Assert.Null(await settings.GetAsync(uow, a));
            Assert.Equal(1, await settings.PutAsync(uow, a, "{\"volume\":5,\"keys\":{\"cast\":\"K\"}}", t0));
            Assert.Equal(1, await settings.PutAsync(uow, a, "{\"volume\":7}", t0.AddMinutes(1)));
            AccountSettingsRow row = (await settings.GetAsync(uow, a))!;
            Assert.Equal(a, row.AccountId);
            Assert.Equal("{\"volume\": 7}", row.Settings);
            Assert.Equal(t0.AddMinutes(1), row.UpdatedAt);
            Assert.Equal(1L, await AccountsKit.ScalarAsync<long>(uow, "SELECT count(*) FROM account_settings"));
        }

        // 지금 걸려 있는 제재만(시작했고, 영구이거나 아직 끝나지 않음). 해제는 그 시각으로 당긴다. 이력은 남는다
        [Fact]
        public async Task Sanctions_ActiveListing_AndEnding()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var sanctions = new PgSanctionStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long chat = await sanctions.InsertAsync(uow, new NewSanction(5, SanctionKind.Chat, "abuse", t0, t0.AddDays(1), "operator:admin"));
            long login = await sanctions.InsertAsync(uow, new NewSanction(5, SanctionKind.Login, "cheat", t0, null, "system"));
            long future = await sanctions.InsertAsync(uow, new NewSanction(5, SanctionKind.Trade, "rmt", t0.AddDays(2), t0.AddDays(3), "operator:admin"));
            await sanctions.InsertAsync(uow, new NewSanction(6, SanctionKind.Duel, "x", t0, null, "system"));
            Assert.Equal(new[] { chat, login }, (await sanctions.ListActiveAsync(uow, 5, t0.AddHours(1))).Select(s => s.Id).ToArray());
            Assert.Equal(new[] { login }, (await sanctions.ListActiveAsync(uow, 5, t0.AddDays(1))).Select(s => s.Id).ToArray());
            Assert.Equal(new[] { login, future }, (await sanctions.ListActiveAsync(uow, 5, t0.AddDays(2))).Select(s => s.Id).ToArray());
            SanctionRow l = (await sanctions.GetAsync(uow, login))!;
            Assert.Equal(SanctionKind.Login, l.SanctionKind);
            Assert.Equal("cheat", l.Reason);
            Assert.Equal("system", l.By);
            Assert.Null(l.Until);
            Assert.True(await sanctions.EndAsync(uow, login, t0.AddHours(2)));
            Assert.False(await sanctions.EndAsync(uow, login, t0.AddHours(3)));       // 이미 끝났다
            Assert.False(await sanctions.EndAsync(uow, future, t0.AddHours(3)));      // 시작 전 제재는 당길 수 없다
            Assert.Equal(new[] { chat }, (await sanctions.ListActiveAsync(uow, 5, t0.AddHours(2))).Select(s => s.Id).ToArray());
            Assert.Equal(3, (await sanctions.ListAllAsync(uow, 5)).Count);
            Assert.Throws<ArgumentException>(() => new NewSanction(5, SanctionKind.Chat, "r", t0, t0, "system"));
        }
    }
}
