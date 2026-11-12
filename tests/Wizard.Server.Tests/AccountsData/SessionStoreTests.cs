using System;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // PgSessionStore: spec 12 2.3절 세션 상태 기계의 저장 쪽(접근 확인, 회전, 쓴 갱신 토큰 기록, 재사용 탐지의 바탕, 폐기, 정리)
    public sealed class SessionStoreTests
    {
        [Fact]
        public async Task Insert_ThenFindActiveByAccess_UntilTheAccessExpiresOrIsRevoked()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long account, long device) = await SetupAsync(db);
            var sessions = new PgSessionStore();
            DateTime now = db.Clock.UtcNow;
            Guid family = Guid.NewGuid();
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long id = await sessions.InsertAsync(uow, New(account, device, "a1", "r1", now, family));
            SessionRow s = (await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a1"), now))!;
            Assert.Equal(id, s.Id);
            Assert.Equal(account, s.AccountId);
            Assert.Equal(device, s.DeviceId);
            Assert.Equal(family, s.FamilyId);
            Assert.Equal(now.AddMinutes(30), s.AccessExpiresAt);
            Assert.Equal(now.AddDays(30), s.ExpiresAt);
            Assert.Null(s.RevokedAt);
            Assert.Null(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a1"), now.AddMinutes(30)));
            Assert.Null(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("nope"), now));
            Assert.True(await sessions.RevokeAsync(uow, id, now));
            Assert.False(await sessions.RevokeAsync(uow, id, now));
            Assert.Null(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a1"), now));
        }

        // 회전: 옛 갱신 해시는 session_refresh_used 로 가고 세션은 새 두 해시와 새 만료(슬라이딩)를 가진다
        [Fact]
        public async Task Rotate_MovesTheOldRefreshHashToTheUsedTable()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long account, long device) = await SetupAsync(db);
            var sessions = new PgSessionStore();
            DateTime now = db.Clock.UtcNow;
            DateTime later = now.AddDays(2);
            Guid family = Guid.NewGuid();
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long id = await sessions.InsertAsync(uow, New(account, device, "a1", "r1", now, family));
            SessionRow s = (await sessions.LockByRefreshHashAsync(uow, AccountsKit.Hash("r1")))!;
            Assert.True(await sessions.RotateAsync(uow, s.Id, AccountsKit.Hash("a2"), later.AddMinutes(30), AccountsKit.Hash("r2"), later.AddDays(30), later));
            Assert.Null(await sessions.LockByRefreshHashAsync(uow, AccountsKit.Hash("r1")));
            SessionRow rotated = (await sessions.LockByRefreshHashAsync(uow, AccountsKit.Hash("r2")))!;
            Assert.Equal(id, rotated.Id);
            Assert.Equal(AccountsKit.Hash("a2"), rotated.AccessHash);
            Assert.Equal(later.AddDays(30), rotated.ExpiresAt);
            Assert.Equal(later.AddMinutes(30), rotated.AccessExpiresAt);
            Assert.Null(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a1"), now));
            RefreshUsedRow used = (await sessions.FindRefreshUsedAsync(uow, AccountsKit.Hash("r1")))!;
            Assert.Equal(family, used.FamilyId);
            Assert.Equal(account, used.AccountId);
            Assert.Equal(device, used.DeviceId);
            Assert.Equal(later, used.At);
            Assert.Null(await sessions.FindRefreshUsedAsync(uow, AccountsKit.Hash("r2")));
        }

        [Fact]
        public async Task Rotate_RevokedSession_ChangesNothing()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long account, long device) = await SetupAsync(db);
            var sessions = new PgSessionStore();
            DateTime now = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long id = await sessions.InsertAsync(uow, New(account, device, "a1", "r1", now, Guid.NewGuid()));
            Assert.True(await sessions.RevokeAsync(uow, id, now));
            Assert.False(await sessions.RotateAsync(uow, id, AccountsKit.Hash("a2"), now, AccountsKit.Hash("r2"), now, now));
            Assert.Null(await sessions.FindRefreshUsedAsync(uow, AccountsKit.Hash("r1")));
            Assert.NotNull(await sessions.LockByRefreshHashAsync(uow, AccountsKit.Hash("r1")));
        }

        // 같은 갱신 토큰으로 동시에 둘이 갱신하면 하나만 회전한다. 뒤의 것은 null 을 받고 쓴 토큰 기록에서 재사용이 드러난다(2.3절)
        [Fact]
        public async Task ConcurrentRefresh_WithTheSameToken_RotatesOnce_AndTheOtherSeesAReuse()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long account, long device) = await SetupAsync(db);
            var sessions = new PgSessionStore();
            DateTime now = db.Clock.UtcNow;
            Guid family = Guid.NewGuid();
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                await sessions.InsertAsync(uow, New(account, device, "a1", "r1", now, family));
                await uow.CommitAsync(AccountsKit.None);
            }
            await using IUnitOfWork a = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork b = await db.App.BeginAsync(AccountsKit.None);
            SessionRow sa = (await sessions.LockByRefreshHashAsync(a, AccountsKit.Hash("r1")))!;
            Task<SessionRow?> sb = sessions.LockByRefreshHashAsync(b, AccountsKit.Hash("r1"));
            await Task.Delay(200);
            Assert.False(sb.IsCompleted);
            Assert.True(await sessions.RotateAsync(a, sa.Id, AccountsKit.Hash("a2"), now.AddMinutes(30), AccountsKit.Hash("r2"), now.AddDays(30), now));
            await a.CommitAsync(AccountsKit.None);
            Assert.Null(await sb);
            RefreshUsedRow reuse = (await sessions.FindRefreshUsedAsync(b, AccountsKit.Hash("r1")))!;
            Assert.Equal(family, reuse.FamilyId);
            Assert.Equal(device, reuse.DeviceId);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM session_refresh_used"));
        }

        // 폐기 범위: 한 계열(family), 한 기기(device_id), 한 계정 전부. 다른 계정의 세션은 그대로
        [Fact]
        public async Task Revoke_ByFamilyDeviceAndAccount()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            EmailProtector protector = AccountsKit.NewProtector();
            var accounts = new PgAccountStore(protector);
            var devices = new PgDeviceStore();
            var sessions = new PgSessionStore();
            long a = await AccountsKit.CreateAsync(db, accounts, "user0001", "Nick1", "x@y.co");
            long b = await AccountsKit.CreateAsync(db, accounts, "user0002", "Nick2", "z@y.co");
            DateTime now = db.Clock.UtcNow;
            Guid f1 = Guid.NewGuid();
            Guid f2 = Guid.NewGuid();
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long phone = (await devices.TouchAsync(uow, a, AccountsKit.Hash("phone"), now)).DeviceId;
            long laptop = (await devices.TouchAsync(uow, a, AccountsKit.Hash("laptop"), now)).DeviceId;
            long bDevice = (await devices.TouchAsync(uow, b, AccountsKit.Hash("phone"), now)).DeviceId;
            await sessions.InsertAsync(uow, New(a, phone, "a1", "r1", now, f1));
            await sessions.InsertAsync(uow, New(a, phone, "a2", "r2", now, f2));
            await sessions.InsertAsync(uow, New(a, laptop, "a3", "r3", now, Guid.NewGuid()));
            await sessions.InsertAsync(uow, New(b, bDevice, "a4", "r4", now, Guid.NewGuid()));
            Assert.Equal(1, await sessions.RevokeFamilyAsync(uow, f1, now));
            Assert.Equal(1, await sessions.RevokeDeviceAsync(uow, phone, now));     // f2 만 남아 있었다
            Assert.NotNull(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a3"), now));
            Assert.Equal(1, await sessions.RevokeAccountAsync(uow, a, now));        // laptop
            Assert.Equal(0, await sessions.RevokeAccountAsync(uow, a, now));
            Assert.NotNull(await sessions.FindActiveByAccessHashAsync(uow, AccountsKit.Hash("a4"), now));
        }

        [Fact]
        public async Task Cleanup_DeletesLongExpiredOrRevokedSessions_AndOldUsedRefreshHashes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            (long account, long device) = await SetupAsync(db);
            var sessions = new PgSessionStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long revoked = await sessions.InsertAsync(uow, New(account, device, "a1", "r1", t0, Guid.NewGuid()));
            long rotated = await sessions.InsertAsync(uow, New(account, device, "a2", "r2", t0, Guid.NewGuid()));
            Assert.True(await sessions.RevokeAsync(uow, revoked, t0));
            Assert.True(await sessions.RotateAsync(uow, rotated, AccountsKit.Hash("a3"), t0.AddDays(40).AddMinutes(30), AccountsKit.Hash("r3"), t0.AddDays(70), t0.AddDays(40)));
            Assert.Equal(1, await sessions.DeleteSettledBeforeAsync(uow, t0.AddDays(1)));
            Assert.Null(await sessions.GetAsync(uow, revoked));
            Assert.NotNull(await sessions.GetAsync(uow, rotated));
            Assert.Equal(0, await sessions.DeleteRefreshUsedBeforeAsync(uow, t0.AddDays(40)));
            Assert.Equal(1, await sessions.DeleteRefreshUsedBeforeAsync(uow, t0.AddDays(40).AddSeconds(1)));
        }

        private static async Task<(long, long)> SetupAsync(PgTestDatabase db)
        {
            long account = await AccountsKit.CreateAsync(db, new PgAccountStore(AccountsKit.NewProtector()), "user0001", "Nick1", "x@y.co");
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            long device = (await new PgDeviceStore().TouchAsync(uow, account, AccountsKit.Hash("device"), db.Clock.UtcNow)).DeviceId;
            await uow.CommitAsync(AccountsKit.None);
            return (account, device);
        }

        // 접근 30분, 갱신 30일(spec 12 2.3절)
        private static NewSession New(long account, long device, string access, string refresh, DateTime now, Guid family) =>
            new NewSession(account, device, AccountsKit.Hash(access), now.AddMinutes(30), AccountsKit.Hash(refresh), now.AddDays(30), family);
    }
}
