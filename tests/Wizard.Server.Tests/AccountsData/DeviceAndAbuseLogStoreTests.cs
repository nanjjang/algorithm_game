using System;
using System.Net;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // PgDeviceStore(2.6절 처음 보는 기기), PgLoginAttemptStore(2.4절 연속 실패), PgSignupLogStore(2.5절), PgRecoveryRequestLogStore(2.11~2.12절)
    public sealed class DeviceAndAbuseLogStoreTests
    {
        private static readonly IPAddress Ip1 = IPAddress.Parse("198.51.100.7");
        private static readonly IPAddress Ip2 = IPAddress.Parse("2001:db8::1");

        [Fact]
        public async Task Device_FirstTouchIsNew_LaterTouchesAreNot_AndLastSeenMovesForward()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var accounts = new PgAccountStore(AccountsKit.NewProtector());
            long a = await AccountsKit.CreateAsync(db, accounts, "user0001", "Nick1", "x@y.co");
            long b = await AccountsKit.CreateAsync(db, accounts, "user0002", "Nick2", "x@y.co");
            var devices = new PgDeviceStore();
            DateTime t0 = db.Clock.UtcNow;
            byte[] phone = AccountsKit.Hash("phone");
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            DeviceSeen first = await devices.TouchAsync(uow, a, phone, t0);
            Assert.True(first.IsNew);
            DeviceSeen again = await devices.TouchAsync(uow, a, phone, t0.AddHours(1));
            Assert.False(again.IsNew);
            Assert.Equal(first.DeviceId, again.DeviceId);
            await devices.TouchAsync(uow, a, phone, t0.AddMinutes(5));   // 늦게 온 요청이 last_seen 을 되돌리지 않는다
            Assert.Equal(t0.AddHours(1), await AccountsKit.ScalarAsync<DateTime>(uow, "SELECT last_seen FROM device WHERE id = @Id", new { Id = first.DeviceId }));
            Assert.Equal(t0, await AccountsKit.ScalarAsync<DateTime>(uow, "SELECT first_seen FROM device WHERE id = @Id", new { Id = first.DeviceId }));
            DeviceSeen otherAccount = await devices.TouchAsync(uow, b, phone, t0);   // 같은 기기라도 다른 계정에는 처음이다
            Assert.True(otherAccount.IsNew);
            Assert.NotEqual(first.DeviceId, otherAccount.DeviceId);
            Assert.Equal(2, await devices.CountAccountsAsync(uow, phone));
            Assert.Equal(new[] { a, b }, await devices.ListAccountsAsync(uow, phone));
            Assert.Equal(0, await devices.CountAccountsAsync(uow, AccountsKit.Hash("other")));
        }

        // 같은 계정·기기의 동시 로그인 둘: 행은 하나, "처음 보는 기기"는 한쪽만
        [Fact]
        public async Task Device_ConcurrentFirstTouches_MakeOneRow_AndOneIsNew()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long a = await AccountsKit.CreateAsync(db, new PgAccountStore(AccountsKit.NewProtector()), "user0001", "Nick1", "x@y.co");
            var devices = new PgDeviceStore();
            byte[] phone = AccountsKit.Hash("phone");
            await using IUnitOfWork u1 = await db.App.BeginAsync(AccountsKit.None);
            await using IUnitOfWork u2 = await db.App.BeginAsync(AccountsKit.None);
            DeviceSeen s1 = await devices.TouchAsync(u1, a, phone, db.Clock.UtcNow);
            Task<DeviceSeen> s2 = devices.TouchAsync(u2, a, phone, db.Clock.UtcNow);
            await Task.Delay(200);
            Assert.False(s2.IsCompleted);
            await u1.CommitAsync(AccountsKit.None);
            DeviceSeen r2 = await s2;
            await u2.CommitAsync(AccountsKit.None);
            Assert.True(s1.IsNew);
            Assert.False(r2.IsNew);
            Assert.Equal(s1.DeviceId, r2.DeviceId);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM device"));
        }

        // 2.4절: 마지막 성공 이후의 연속 실패 수와 마지막 실패 시각. 성공하면 다시 0부터
        [Fact]
        public async Task LoginAttempts_CountFailuresSinceTheLastSuccess_AndPerIpWindows()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var attempts = new PgLoginAttemptStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            LoginFailureState none = await attempts.GetFailureStateAsync(uow, "ghost123");
            Assert.Equal(0, none.ConsecutiveFailures);
            Assert.Null(none.LastFailureAt);
            await attempts.InsertAsync(uow, "mage01", Ip1, t0, false);
            await attempts.InsertAsync(uow, "mage01", Ip1, t0.AddSeconds(1), false);
            await attempts.InsertAsync(uow, "mage01", Ip2, t0.AddSeconds(2), true);
            for (int i = 0; i < 6; i++)
            {
                await attempts.InsertAsync(uow, "mage01", Ip1, t0.AddSeconds(10 + i), false);
            }
            await attempts.InsertAsync(uow, "ghost123", Ip1, t0.AddSeconds(20), false);   // 없는 아이디도 같은 방식으로 센다
            LoginFailureState s = await attempts.GetFailureStateAsync(uow, "mage01");
            Assert.Equal(6, s.ConsecutiveFailures);
            Assert.Equal(t0.AddSeconds(15), s.LastFailureAt);
            Assert.Equal(1, (await attempts.GetFailureStateAsync(uow, "ghost123")).ConsecutiveFailures);
            await attempts.InsertAsync(uow, "mage01", Ip1, t0.AddSeconds(30), true);
            Assert.Equal(0, (await attempts.GetFailureStateAsync(uow, "mage01")).ConsecutiveFailures);
            // IP당 분당: 지난 60초
            Assert.Equal(10, await attempts.CountByIpSinceAsync(uow, Ip1, t0.AddSeconds(30).AddSeconds(-60)));
            Assert.Equal(2, await attempts.CountByIpSinceAsync(uow, Ip1, t0.AddSeconds(15)));
            Assert.Equal(1, await attempts.CountByIpSinceAsync(uow, Ip2, t0.AddSeconds(-1)));
            Assert.Equal(3, await attempts.DeleteBeforeAsync(uow, t0.AddSeconds(10)));
        }

        // 2.5절: IP·기기당 지난 24시간 가입 수 [임시: 이동 창]
        [Fact]
        public async Task SignupLog_CountsPerIpAndDevice_InTheWindow()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            var log = new PgSignupLogStore();
            DateTime t0 = db.Clock.UtcNow;
            byte[] d1 = AccountsKit.Hash("d1");
            byte[] d2 = AccountsKit.Hash("d2");
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            await log.InsertAsync(uow, Ip1, d1, t0);
            await log.InsertAsync(uow, Ip1, d2, t0.AddHours(1));
            await log.InsertAsync(uow, Ip2, d1, t0.AddHours(2));
            DateTime now = t0.AddHours(24).AddMinutes(30);
            Assert.Equal(1, await log.CountByIpSinceAsync(uow, Ip1, now.AddHours(-24)));
            Assert.Equal(2, await log.CountByIpSinceAsync(uow, Ip1, t0.AddSeconds(-1)));
            Assert.Equal(1, await log.CountByDeviceSinceAsync(uow, d1, now.AddHours(-24)));
            Assert.Equal(2, await log.CountByDeviceSinceAsync(uow, d1, t0.AddSeconds(-1)));
            Assert.Equal(1, await log.DeleteBeforeAsync(uow, t0.AddMinutes(1)));
        }

        // 2.11~2.12절: 재설정 요청과 아이디 찾기를 계정당·IP당 합쳐 센다. 계정이 없는 요청은 IP 한도에만 든다
        [Fact]
        public async Task RecoveryLog_CountsBothKindsTogether_PerAccountAndPerIp()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long a = await AccountsKit.CreateAsync(db, new PgAccountStore(AccountsKit.NewProtector()), "user0001", "Nick1", "x@y.co");
            var log = new PgRecoveryRequestLogStore();
            DateTime t0 = db.Clock.UtcNow;
            await using IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None);
            await log.InsertAsync(uow, a, Ip1, RecoveryRequestKind.ResetPassword, t0);
            await log.InsertAsync(uow, a, Ip2, RecoveryRequestKind.FindLoginIds, t0.AddMinutes(10));
            await log.InsertAsync(uow, null, Ip1, RecoveryRequestKind.ResetPassword, t0.AddMinutes(20));
            Assert.Equal(2, await log.CountByAccountSinceAsync(uow, a, t0.AddSeconds(-1)));
            Assert.Equal(1, await log.CountByAccountSinceAsync(uow, a, t0.AddMinutes(10).AddSeconds(-60)));
            Assert.Equal(2, await log.CountByIpSinceAsync(uow, Ip1, t0.AddSeconds(-1)));
            Assert.Equal(1, await log.CountByIpSinceAsync(uow, Ip2, t0.AddSeconds(-1)));
            Assert.Equal(2, await log.DeleteBeforeAsync(uow, t0.AddMinutes(15)));
        }
    }
}
