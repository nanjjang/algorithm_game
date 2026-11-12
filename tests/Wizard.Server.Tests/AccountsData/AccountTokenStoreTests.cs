using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // PgAccountTokenStore: spec 12 2.9~2.11절의 코드 행(해시만, 가장 새 활성 코드, 틀림 5회 폐기, 한 번만, 앞 코드 폐기, 정리)
    public sealed class AccountTokenStoreTests
    {
        private const short DiscardAt = 5;   // spec 12 2.9절·2.11절 "5번 틀리면 폐기"

        [Fact]
        public async Task Insert_ThenLockLatestActive_ReturnsTheNewestUnusedUnexpiredCode()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long id, PgAccountTokenStore tokens) = await SetupAsync(db);
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long older = await tokens.InsertAsync(uow, Verify(id, "111111", now.AddMinutes(15)), now);
            long newer = await tokens.InsertAsync(uow, Verify(id, "222222", now.AddMinutes(15)), now.AddSeconds(61));
            await tokens.InsertAsync(uow, Reset(id, "ABCDEFGHJKLM", now.AddMinutes(30)), now);
            AccountTokenRow t = (await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.VerifyEmail, now.AddSeconds(62)))!;
            Assert.Equal(newer, t.Id);
            Assert.Equal(AccountTokenKind.VerifyEmail, t.TokenKind);
            Assert.Equal(AccountsKit.Hash("222222"), t.TokenHash);
            Assert.Equal((short)0, t.Attempts);
            Assert.Null(t.NewEmailEnc);
            Assert.NotEqual(older, t.Id);
            // 만료된 코드는 잡히지 않는다
            Assert.Null(await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.VerifyEmail, now.AddMinutes(16)));
            Assert.Equal(2, await tokens.CountCreatedSinceAsync(uow, id, AccountTokenKind.VerifyEmail, now.AddSeconds(-1)));
            Assert.Equal(1, await tokens.CountCreatedSinceAsync(uow, id, AccountTokenKind.VerifyEmail, now));
        }

        // 2.9절 [임시]·2.11절: 새 코드를 보내면 같은 계정·종류의 미사용 코드를 전부 폐기한다(다른 종류는 그대로)
        [Fact]
        public async Task RevokeActive_DiscardsOnlyThatKindsUnusedCodes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long id, PgAccountTokenStore tokens) = await SetupAsync(db);
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            await tokens.InsertAsync(uow, Reset(id, "AAAAAAAAAAAA", now.AddMinutes(30)), now);
            await tokens.InsertAsync(uow, Reset(id, "BBBBBBBBBBBB", now.AddMinutes(30)), now);
            long verify = await tokens.InsertAsync(uow, Verify(id, "123456", now.AddMinutes(15)), now);
            Assert.Equal(2, await tokens.RevokeActiveAsync(uow, id, AccountTokenKind.ResetPassword, now.AddSeconds(1)));
            Assert.Equal(0, await tokens.RevokeActiveAsync(uow, id, AccountTokenKind.ResetPassword, now.AddSeconds(2)));
            Assert.Null(await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.ResetPassword, now));
            Assert.Equal(verify, (await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.VerifyEmail, now))!.Id);
        }

        // 틀림은 코드 단위로 센다. 다섯 번째 틀림에서 그 자리에서 폐기되고, 폐기된 코드의 틀림은 더 세지 않는다
        [Fact]
        public async Task WrongAttempts_DiscardTheCodeAtTheFifth()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long id, PgAccountTokenStore tokens) = await SetupAsync(db);
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long t = await tokens.InsertAsync(uow, Verify(id, "123456", now.AddMinutes(15)), now);
            for (short i = 1; i < DiscardAt; i++)
            {
                Assert.Equal(i, await tokens.RecordWrongAttemptAsync(uow, t, DiscardAt, now));
                Assert.NotNull(await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.VerifyEmail, now));
            }
            Assert.Equal(DiscardAt, await tokens.RecordWrongAttemptAsync(uow, t, DiscardAt, now.AddSeconds(5)));
            AccountTokenRow row = (await tokens.GetAsync(uow, t))!;
            Assert.Equal(now.AddSeconds(5), row.UsedAt);
            Assert.Null(await tokens.LockLatestActiveAsync(uow, id, AccountTokenKind.VerifyEmail, now));
            Assert.Null(await tokens.RecordWrongAttemptAsync(uow, t, DiscardAt, now));
            Assert.False(await tokens.MarkUsedAsync(uow, t, now));
        }

        // 한 번만: 같은 코드를 두 번 쓰면 두 번째는 false. 동시에 둘이 쓰려 하면 뒤의 것은 앞의 커밋을 기다린 뒤 활성 코드를 못 찾는다
        [Fact]
        public async Task MarkUsed_IsOnce_EvenUnderConcurrentConfirms()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long id, PgAccountTokenStore tokens) = await SetupAsync(db);
            DateTime now = db.Clock.UtcNow;
            long t;
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                t = await tokens.InsertAsync(uow, Reset(id, "ABCDEFGHJKLM", now.AddMinutes(30)), now);
                await uow.CommitAsync(AccountsKit.None);
            }
            await using IUnitOfWork a = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork b = await db.App.BeginAsync(AccountsKit.None);
            Assert.Equal(t, (await tokens.LockLatestActiveAsync(a, id, AccountTokenKind.ResetPassword, now))!.Id);
            Task<AccountTokenRow?> second = tokens.LockLatestActiveAsync(b, id, AccountTokenKind.ResetPassword, now);
            await Task.Delay(200);
            Assert.False(second.IsCompleted);
            Assert.True(await tokens.MarkUsedAsync(a, t, now));
            await a.CommitAsync(AccountsKit.None);
            Assert.Null(await second);
            Assert.False(await tokens.MarkUsedAsync(b, t, now));
        }

        // 2.10절: 바꿀 주소는 암호문으로만. 같은 키·같은 AAD(계정 번호)라 다른 계정의 행으로 옮기면 풀리지 않는다
        [Fact]
        public async Task ChangeEmailToken_StoresOnlyTheEncryptedNewAddress()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            EmailProtector protector = AccountsKit.NewProtector();
            var accounts = new PgAccountStore(protector);
            var tokens = new PgAccountTokenStore(protector);
            long id = await AccountsKit.CreateAsync(db, accounts, "user0001", "Nick1", "old@y.co");
            long other = await AccountsKit.CreateAsync(db, accounts, "user0002", "Nick2", "z@y.co");
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long t = await tokens.InsertAsync(uow,
                new NewAccountToken(id, AccountTokenKind.ChangeEmail, AccountsKit.Hash("654321"), now.AddMinutes(15), IPAddress.Parse("203.0.113.9"), "New@Mail.co"), now);
            AccountTokenRow row = (await tokens.GetAsync(uow, t))!;
            Assert.NotNull(row.NewEmailEnc);
            Assert.Equal("New@Mail.co", tokens.DecryptNewEmail(row));
            row.AccountId = other;
            Assert.ThrowsAny<CryptographicException>(() => tokens.DecryptNewEmail(row));
            Assert.Equal("203.0.113.9", await AccountsKit.ScalarAsync<string>(uow, "SELECT host(created_ip) FROM account_token WHERE id = @Id", new { Id = t }));
        }

        [Fact]
        public void NewToken_NewEmailGoesWithChangeEmailOnly()
        {
            DateTime at = DateTime.UtcNow;
            Assert.Throws<ArgumentException>(() => new NewAccountToken(1, AccountTokenKind.VerifyEmail, new byte[] { 1 }, at, null, "a@b.co"));
            Assert.Throws<ArgumentException>(() => new NewAccountToken(1, AccountTokenKind.ChangeEmail, new byte[] { 1 }, at, null, null));
            Assert.Throws<ArgumentException>(() => new NewAccountToken(1, AccountTokenKind.ResetPassword, Array.Empty<byte>(), at, null, null));
        }

        // 8.4절: 쓰거나 만료된 코드는 24시간 뒤 지운다. online 10: IP 는 30일 뒤 NULL 로 덮는다 [임시]
        [Fact]
        public async Task Cleanup_DeletesSettledCodes_AndClearsOldIps()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long id, PgAccountTokenStore tokens) = await SetupAsync(db);
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long used = await tokens.InsertAsync(uow, Verify(id, "1", t0.AddMinutes(15)), t0);
            long expired = await tokens.InsertAsync(uow, Verify(id, "2", t0.AddMinutes(15)), t0);
            long alive = await tokens.InsertAsync(uow, Reset(id, "3", t0.AddDays(3)), t0);
            Assert.True(await tokens.MarkUsedAsync(uow, used, t0.AddMinutes(1)));
            DateTime cutoff = t0.AddMinutes(15).AddHours(24).AddSeconds(1);   // now − 24h
            Assert.Equal(2, await tokens.DeleteSettledBeforeAsync(uow, cutoff));
            Assert.Null(await tokens.GetAsync(uow, used));
            Assert.Null(await tokens.GetAsync(uow, expired));
            Assert.NotNull(await tokens.GetAsync(uow, alive));
            Assert.Equal(0, await tokens.ClearCreatedIpBeforeAsync(uow, t0));
            Assert.Equal(1, await tokens.ClearCreatedIpBeforeAsync(uow, t0.AddDays(30)));
            Assert.Equal(0L, await AccountsKit.ScalarAsync<long>(uow, "SELECT count(*) FROM account_token WHERE created_ip IS NOT NULL"));
        }

        private static async Task<(long, PgAccountTokenStore)> SetupAsync(PgTestDatabase db)
        {
            EmailProtector protector = AccountsKit.NewProtector();
            long id = await AccountsKit.CreateAsync(db, new PgAccountStore(protector), "user0001", "Nick1", "x@y.co");
            return (id, new PgAccountTokenStore(protector));
        }

        private static NewAccountToken Verify(long accountId, string code, DateTime expiresAt) =>
            new NewAccountToken(accountId, AccountTokenKind.VerifyEmail, AccountsKit.Hash(code), expiresAt, IPAddress.Loopback, null);

        private static NewAccountToken Reset(long accountId, string code, DateTime expiresAt) =>
            new NewAccountToken(accountId, AccountTokenKind.ResetPassword, AccountsKit.Hash(code), expiresAt, IPAddress.IPv6Loopback, null);
    }
}
