using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // 진짜 account 표에서 "번호가 정해진 계정 행"을 만들고, M10-DB-01 의 IRowLocks.LockAccountsAsync 가 그 행을 잠그는지 본다.
    // 뒤에 저장소가 받는 번호는 그 번호들과 겹치지 않는다
    public sealed class AccountTestRowsTests
    {
        [Fact]
        public async Task FixedIdRows_AreLockable_AndLaterSignupsGetFreshIds()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await AccountTestRows.EnsureAsync(db, 3, 7, 1234567890123456789);
            await AccountTestRows.EnsureAsync(db, 7);   // 같은 번호를 다시 줘도 그대로
            Assert.Equal(3L, await db.ScalarAsync<long>("SELECT count(*) FROM account"));
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                await db.Locks.LockAccountsAsync(uow, 7, 3);
                await uow.CommitAsync(AccountsKit.None);
            }
            var store = new PgAccountStore(AccountsKit.NewProtector());
            long fresh = await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co");
            Assert.True(fresh > 1234567890123456789);
        }

        [Fact]
        public async Task SmallIds_StayWithinTheLoginIdRule()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await AccountTestRows.EnsureAsync(db, 1, 2);
            Assert.Equal(new[] { "t001", "t002" }, (await db.QueryAsync<string>("SELECT login_id FROM account ORDER BY id")).ToArray());
            var store = new PgAccountStore(AccountsKit.NewProtector());
            Assert.Equal(3L, await AccountsKit.CreateAsync(db, store, "user0001", "Nick1", "x@y.co"));
        }
    }
}
