using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // PgAccountStore: 가입(유일성, 동시 가입), 주소 하나에 3계정의 셈, 읽기·잠금, 비밀번호·주소·보류·삭제·캐시 열
    public sealed class AccountStoreTests
    {
        [Fact]
        public async Task Create_StoresTheRow_WithServerTimeAndEncryptedEmail()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            EmailProtector protector = AccountsKit.NewProtector();
            var store = new PgAccountStore(protector);
            long id = await AccountsKit.CreateAsync(db, store, "mage01", "Apprentice", "Mage@Example.com");

            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            AccountRow row = (await store.GetAsync(uow, id))!;
            Assert.Equal("mage01", row.LoginId);
            Assert.Equal("Apprentice", row.Nickname);
            Assert.Equal(AccountsKit.FakePwHash("mage01"), row.PwHash);
            Assert.Contains("argon2id", row.PwParams, StringComparison.Ordinal);
            Assert.Equal(db.Clock.UtcNow, row.CreatedAt);
            Assert.Null(row.EmailVerifiedAt);
            Assert.Equal("Mage@Example.com", store.DecryptEmail(row));
            Assert.Equal(protector.LookupHmac("mage@example.com"), row.EmailHmac);
            Assert.Equal(id, (await store.FindByLoginIdAsync(uow, "mage01"))!.Id);
            Assert.Null(await store.FindByLoginIdAsync(uow, "nobody"));
            Assert.Null(await store.GetAsync(uow, id + 1000));
        }

        [Fact]
        public async Task Create_TakenLoginIdOrNickname_IsAValue_AndTheTransactionStaysUsable()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            await AccountsKit.CreateAsync(db, store, "mage01", "Apprentice", "a@b.co");

            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            AccountCreateResult sameId = await store.CreateAsync(uow, AccountsKit.Account("mage01", "Other", "c@d.co"), db.Clock.UtcNow);
            Assert.Equal(AccountCreateOutcome.LoginIdTaken, sameId.Outcome);
            Assert.Equal(0L, sameId.AccountId);
            AccountCreateResult sameNick = await store.CreateAsync(uow, AccountsKit.Account("mage02", "APPRENTICE", "c@d.co"), db.Clock.UtcNow);
            Assert.Equal(AccountCreateOutcome.NicknameTaken, sameNick.Outcome);
            Assert.True(await store.LoginIdExistsAsync(uow, "mage01"));
            Assert.True(await store.NicknameTakenAsync(uow, "apprentice"));
            Assert.False(await store.NicknameTakenAsync(uow, "Novice"));
            AccountCreateResult ok = await store.CreateAsync(uow, AccountsKit.Account("mage02", "Novice", "c@d.co"), db.Clock.UtcNow);
            Assert.True(ok.Created);
            await uow.CommitAsync(AccountsKit.None);
            Assert.Equal(2L, await db.ScalarAsync<long>("SELECT count(*) FROM account"));
        }

        // 같은 아이디의 동시 가입 둘: 하나만 만들어지고 다른 하나는 LoginIdTaken(오류로 트랜잭션이 깨지지 않는다)
        [Fact]
        public async Task ConcurrentSignups_WithTheSameLoginId_CreateExactlyOne()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            await using IUnitOfWork a = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork b = await db.App.BeginAsync(AccountsKit.None);
            AccountCreateResult first = await store.CreateAsync(a, AccountsKit.Account("samename", "NickA", "a@b.co"), db.Clock.UtcNow);
            Task<AccountCreateResult> second = store.CreateAsync(b, AccountsKit.Account("samename", "NickB", "c@d.co"), db.Clock.UtcNow);
            await Task.Delay(200);
            Assert.False(second.IsCompleted);   // 유일 색인에서 a 를 기다린다
            await a.CommitAsync(AccountsKit.None);
            Assert.True(first.Created);
            Assert.Equal(AccountCreateOutcome.LoginIdTaken, (await second).Outcome);
            await b.CommitAsync(AccountsKit.None);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM account"));
        }

        // spec 12 2.5절 주소 하나에 계정 3개: 이미 2개인 주소로 동시에 둘이 가입하면 셈을 같이 지나치지 않는다
        [Fact]
        public async Task EmailLimit_CountIsSerializedPerAddress_UnderConcurrentSignups()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            const int limit = 3;
            await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "shared@mail.com");
            await AccountsKit.CreateAsync(db, store, "user0002", "Nick2", "SHARED@mail.com ");

            await using IUnitOfWork a = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork b = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(2, await store.LockEmailAndCountAsync(a, "shared@mail.com"));
            Task<int> bCount = store.LockEmailAndCountAsync(b, " Shared@Mail.com");
            await Task.Delay(200);
            Assert.False(bCount.IsCompleted);   // a 가 그 주소의 잠금을 쥐고 있다
            Assert.True((await store.CreateAsync(a, AccountsKit.Account("user0003", "Nick3", "shared@mail.com"), db.Clock.UtcNow)).Created);
            await a.CommitAsync(AccountsKit.None);
            Assert.Equal(limit, await bCount);  // a 가 커밋한 뒤 센다: 이미 3개
            await b.DisposeAsync();

            // 다른 주소는 서로 기다리지 않는다
            await using IUnitOfWork c = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork d = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(0, await store.LockEmailAndCountAsync(c, "one@mail.com"));
            Assert.Equal(0, await store.LockEmailAndCountAsync(d, "two@mail.com").WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ListByEmail_ReturnsTheAccountsOfThatAddress_InIdOrder()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long a = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            long other = await AccountsKit.CreateAsync(db, store, "user0002", "Nick2", "z@y.co");
            long b = await AccountsKit.CreateAsync(db, store, "user0003", "Nick3", "X@Y.CO");
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            Assert.True(await store.MarkEmailVerifiedAsync(uow, b, db.Clock.UtcNow));
            IReadOnlyList<EmailAccount> list = await store.ListByEmailAsync(uow, " x@y.co");
            Assert.Equal(new[] { a, b }, list.Select(r => r.Id).ToArray());
            Assert.Equal(new[] { "user0001", "user0003" }, list.Select(r => r.LoginId).ToArray());
            Assert.Null(list[0].EmailVerifiedAt);
            Assert.Equal(db.Clock.UtcNow, list[1].EmailVerifiedAt);
            Assert.Equal("X@Y.CO", store.Email.Decrypt(b, list[1].EmailEnc));
            Assert.DoesNotContain(other, list.Select(r => r.Id));
        }

        [Fact]
        public async Task Lock_HoldsTheRowUntilCommit()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long id = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            await using IUnitOfWork a = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork b = await db.App.BeginAsync(AccountsKit.None);
            Assert.NotNull(await store.LockByLoginIdAsync(a, "user0001"));
            Task<AccountRow?> waiting = store.LockAsync(b, id);
            await Task.Delay(200);
            Assert.False(waiting.IsCompleted);
            Assert.True(await store.SetLastLoginAsync(a, id, db.Clock.UtcNow));
            await a.CommitAsync(AccountsKit.None);
            Assert.Equal(db.Clock.UtcNow, (await waiting)!.LastLogin);
        }

        [Fact]
        public async Task SetPassword_SetEmail_AndVerify_ChangeOnlyThoseColumns()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            EmailProtector protector = AccountsKit.NewProtector();
            var store = new PgAccountStore(protector);
            long id = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "old@y.co");
            DateTime at = db.Clock.UtcNow.AddHours(1);
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            byte[] oldEnc = (await store.GetAsync(uow, id))!.EmailEnc;
            Assert.True(await store.SetPasswordAsync(uow, id, AccountsKit.Hash("new hash"), "{\"alg\":\"argon2id\",\"t\":4}"));
            Assert.True(await store.SetEmailAsync(uow, id, "New@Z.co", at));
            AccountRow row = (await store.GetAsync(uow, id))!;
            Assert.Equal(AccountsKit.Hash("new hash"), row.PwHash);
            Assert.Contains("\"t\": 4", row.PwParams, StringComparison.Ordinal);
            Assert.Equal("New@Z.co", store.DecryptEmail(row));
            Assert.Equal(protector.LookupHmac("new@z.co"), row.EmailHmac);
            Assert.NotEqual(oldEnc, row.EmailEnc);
            Assert.Equal(at, row.EmailVerifiedAt);
            Assert.Equal("user0001", row.LoginId);
            Assert.False(await store.SetPasswordAsync(uow, id + 99, AccountsKit.Hash("x"), "{}"));
            Assert.False(await store.SetEmailAsync(uow, id + 99, "q@q.co", null));
        }

        // spec 12 2.6절: trade_hold_until = max(trade_hold_until, now + 24h). 줄지 않는다
        [Fact]
        public async Task ExtendTradeHold_KeepsTheLaterOfTheTwo()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long id = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(now.AddHours(24), await store.ExtendTradeHoldAsync(uow, id, now.AddHours(24)));
            Assert.Equal(now.AddHours(24), await store.ExtendTradeHoldAsync(uow, id, now.AddHours(2)));
            Assert.Equal(now.AddHours(30), await store.ExtendTradeHoldAsync(uow, id, now.AddHours(30)));
            Assert.Null(await store.ExtendTradeHoldAsync(uow, id + 99, now));
            await Assert.ThrowsAsync<ArgumentException>(() => store.ExtendTradeHoldAsync(uow, id, DateTime.SpecifyKind(now, DateTimeKind.Local)));
        }

        // spec 12 2.7절: 요청(한 번만) → 취소 → 다시 요청, 7일이 지난 계정 목록, 삭제(따라 지워지는 표는 스키마 시험이 본다)
        [Fact]
        public async Task Deletion_RequestCancelListAndDelete()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long a = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            long b = await AccountsKit.CreateAsync(db, store, "user0002", "Nick2", "x@y.co");
            long c = await AccountsKit.CreateAsync(db, store, "user0003", "Nick3", "x@y.co");
            DateTime t0 = db.Clock.UtcNow;
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                Assert.True(await store.RequestDeletionAsync(uow, a, t0));
                Assert.False(await store.RequestDeletionAsync(uow, a, t0.AddDays(1)));   // 처음 요청 시각을 지킨다
                Assert.True(await store.RequestDeletionAsync(uow, b, t0.AddDays(3)));
                Assert.True(await store.RequestDeletionAsync(uow, c, t0));
                Assert.True(await store.CancelDeletionAsync(uow, c));
                Assert.False(await store.CancelDeletionAsync(uow, c));
                await uow.CommitAsync(AccountsKit.None);
            }
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                Assert.Equal(t0, (await store.GetAsync(uow, a))!.DeletionRequestedAt);
                Assert.Equal(new[] { a }, (await store.ListDeletionDueAsync(uow, t0.AddDays(7).AddDays(-7), 10)).ToArray());
                Assert.Equal(new[] { a, b }, (await store.ListDeletionDueAsync(uow, t0.AddDays(3), 10)).ToArray());
                Assert.Equal(new[] { a }, (await store.ListDeletionDueAsync(uow, t0.AddDays(3), 1)).ToArray());
                Assert.True(await store.DeleteAsync(uow, a));
                Assert.False(await store.DeleteAsync(uow, a));
                await uow.CommitAsync(AccountsKit.None);
            }
            Assert.Equal(2L, await db.ScalarAsync<long>("SELECT count(*) FROM account"));
        }

        [Fact]
        public async Task CacheColumns_PlaySecondsGrow_MaxWaveNeverDrops_FlagsAccumulate()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long id = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            DateTime until = db.Clock.UtcNow.AddDays(3);
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(60L, await store.AddPlaySecondsAsync(uow, id, 60));
            Assert.Equal(60L + 45L, await store.AddPlaySecondsAsync(uow, id, 45));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.AddPlaySecondsAsync(uow, id, -1));
            Assert.Null(await store.AddPlaySecondsAsync(uow, id + 99, 1));
            Assert.Equal(12, await store.RaiseMaxWaveAsync(uow, id, 12));
            Assert.Equal(12, await store.RaiseMaxWaveAsync(uow, id, 9));
            Assert.Equal(1L, await store.AddFlagsAsync(uow, id, 1));
            Assert.Equal(1L | 4L, await store.AddFlagsAsync(uow, id, 4));
            Assert.True(await store.SetSanctionUntilAsync(uow, id, until));
            Assert.True(await store.SetShowNameInAlertsAsync(uow, id, false));
            AccountRow row = (await store.GetAsync(uow, id))!;
            Assert.Equal(until, row.SanctionUntil);
            Assert.False(row.ShowNameInAlerts);
            Assert.True(await store.SetSanctionUntilAsync(uow, id, null));
            Assert.Null((await store.GetAsync(uow, id))!.SanctionUntil);
        }

        // 저장소는 App 역할의 권한만으로 돈다(소유자 권한에 기대지 않는다)
        [Fact]
        public async Task Store_RunsUnderTheAppRole()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var store = new PgAccountStore(AccountsKit.NewProtector());
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(DbRoles.App, await uow.Connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT current_user", transaction: uow.Transaction)));
            Assert.True((await store.CreateAsync(uow, AccountsKit.Account("user0001", "Nick1", "x@y.co"), db.Clock.UtcNow)).Created);
        }
    }
}
