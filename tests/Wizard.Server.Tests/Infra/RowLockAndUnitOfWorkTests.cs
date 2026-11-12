using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Wizard.Core;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // CONTRACTS 7.2 IUnitOfWork·IRowLocks
    public sealed class RowLockAndUnitOfWorkTests
    {
        [Fact]
        public async Task AppUnitOfWork_RunsAsTheAppRole_OwnerDoesNot()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork app = await db.App.BeginAsync(CancellationToken.None))
            {
                Assert.Equal(DbRoles.App, await app.Connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT current_user", transaction: app.Transaction)));
            }
            await using (IUnitOfWork owner = await db.Owner.BeginAsync(CancellationToken.None))
            {
                Assert.NotEqual(DbRoles.App, await owner.Connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT current_user", transaction: owner.Transaction)));
            }
        }

        [Fact]
        public async Task SetLocalRole_DoesNotLeakToThePooledConnection()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using (IUnitOfWork app = await db.App.BeginAsync(CancellationToken.None))
            {
                await app.CommitAsync(CancellationToken.None);
            }
            await using NpgsqlConnection c = await db.DataSource.OpenConnectionAsync();
            Assert.NotEqual(DbRoles.App, await c.ExecuteScalarAsync<string>("SELECT current_user"));
        }

        [Fact]
        public async Task UnitOfWork_IsReadCommitted_AndCommitsOnlyOnce()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            Assert.Equal("read committed", await uow.Connection.ExecuteScalarAsync<string>(new CommandDefinition("SHOW transaction_isolation", transaction: uow.Transaction)));
            await uow.CommitAsync(CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData("wizard_app; DROP TABLE request")]
        [InlineData("Wizard")]
        [InlineData("")]
        public void InvalidSessionRole_IsRejected(string role)
        {
            using NpgsqlDataSource ds = NpgsqlDataSource.Create("Host=127.0.0.1");
            Assert.Throws<ArgumentException>(() => new PgUnitOfWorkFactory(ds, role));
        }

        [Fact]
        public async Task LockAccounts_SortsAndDeduplicates_AndRejectsMissingRows()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await Probes.AddAccountsAsync(db, 3, 7);
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await db.Locks.LockAccountsAsync(uow, 7, 3, 7);
                await uow.CommitAsync(CancellationToken.None);
            }
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                RowNotFoundException ex = await Assert.ThrowsAsync<RowNotFoundException>(() => db.Locks.LockAccountsAsync(uow, 3, 99));
                Assert.Equal("account", ex.Table);
                Assert.Equal(99L, ex.Id);
                Assert.Equal(ErrorCode.NotFound, ex.Code);
            }
            await using (IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None))
            {
                await Assert.ThrowsAsync<ArgumentException>(() => db.Locks.LockAccountsAsync(uow));
            }
        }

        [Fact]
        public async Task LockAccounts_ReversedOrdersFromTwoTransactions_DoNotDeadlock()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await Probes.AddAccountsAsync(db, 3, 7);
            for (int round = 0; round < 20; round++)
            {
                Task a = Task.Run(async () =>
                {
                    await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
                    await db.Locks.LockAccountsAsync(uow, 7, 3);
                    await Task.Delay(5);
                    await uow.CommitAsync(CancellationToken.None);
                });
                Task b = Task.Run(async () =>
                {
                    await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
                    await db.Locks.LockAccountsAsync(uow, 3, 7);
                    await Task.Delay(5);
                    await uow.CommitAsync(CancellationToken.None);
                });
                await Task.WhenAll(a, b);   // 교착이면 PostgreSQL 이 한쪽을 40P01 로 끊어 여기서 던진다
            }
        }

        [Fact]
        public async Task LockWand_ReturnsEveryColumn_AndHoldsTheRow()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            Guid run = Guid.NewGuid();
            long id = await db.ScalarAsync<long>(@"
INSERT INTO wand (owner_id, character, stage, floor_peak, record_peak, drop_count, aura, slots_extra, carve_bonus, mutations, pending_offer,
                  loadout, skin_id, show_scars, origin, owner_count, trade_locked_until, lock_state, pending_run_id, debt_locked, acquired_at, version)
VALUES (7, 3, 9, 8, 10, 2, 150, 1, 300, '[{""id"":1}]', '{""offer"":2}', '{""blocks"":[]}', 'skin.a', true, 'trade', 2,
        '2026-11-13T00:00:00Z', 'duel', @Run, true, '2026-11-01T00:00:00Z', 4)
RETURNING id", new { Run = run });

            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            WandRow w = await db.Locks.LockWandAsync(uow, id);
            Assert.Equal(id, w.Id);
            Assert.Equal(7L, w.OwnerId);
            Assert.Equal((short)3, w.Character);
            Assert.Equal((short)9, w.Stage);
            Assert.Equal((short)8, w.FloorPeak);
            Assert.Equal((short)10, w.RecordPeak);
            Assert.Equal(2, w.DropCount);
            Assert.Equal(150, w.Aura);
            Assert.Equal((short)1, w.SlotsExtra);
            Assert.Equal(300, w.CarveBonus);
            Assert.Equal("[{\"id\": 1}]", w.Mutations);
            Assert.Equal("{\"offer\": 2}", w.PendingOffer);
            Assert.Equal("{\"blocks\": []}", w.Loadout);
            Assert.Equal("skin.a", w.SkinId);
            Assert.True(w.ShowScars);
            Assert.Equal("trade", w.Origin);
            Assert.Equal(2, w.OwnerCount);
            Assert.Equal(new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc), w.TradeLockedUntil!.Value.ToUniversalTime());
            Assert.Equal("duel", w.LockState);
            Assert.Equal(run, w.PendingRunId);
            Assert.True(w.DebtLocked);
            Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), w.AcquiredAt!.Value.ToUniversalTime());
            Assert.Equal(4, w.Version);

            // FOR UPDATE: 다른 트랜잭션은 그 행을 바로 잡지 못한다
            await using IUnitOfWork other = await db.App.BeginAsync(CancellationToken.None);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => other.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT id FROM wand WHERE id = @Id FOR UPDATE NOWAIT", new { Id = id }, other.Transaction)));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, ex.SqlState);
        }

        [Fact]
        public async Task LockWand_Missing_IsRowNotFound()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            RowNotFoundException ex = await Assert.ThrowsAsync<RowNotFoundException>(() => db.Locks.LockWandAsync(uow, 12345));
            Assert.Equal("wand", ex.Table);
        }
    }
}
