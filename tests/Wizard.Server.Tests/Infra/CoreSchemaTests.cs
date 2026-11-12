using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 1·2: 0001_core.sql 이 만드는 표와 열(spec 12 8.2절 + CONTRACTS 12.1절), 원장 권한
    public sealed class CoreSchemaTests
    {
        // 표 → 열(순서 무관). 기대값은 spec 12 8.2절 DDL 과 CONTRACTS 12.1절(request.fingerprint, result NULL 허용)
        private static readonly Dictionary<string, string[]> Expected = new Dictionary<string, string[]>
        {
            ["request"] = new[] { "account_id", "request_id", "kind", "fingerprint", "result", "at" },
            ["currency_balance"] = new[] { "account_id", "currency", "bucket", "amount", "amount_pending" },
            ["currency_ledger"] = new[] { "id", "account_id", "currency", "bucket", "delta", "balance_after", "pending", "reason", "ref_type", "ref_id", "request_id", "at" },
            ["item_ledger"] = new[] { "id", "account_id", "item_ref", "delta", "pending", "reason", "ref_type", "ref_id", "request_id", "at" },
            ["config"] = new[] { "key", "value", "updated_at" },
            ["audit_log"] = new[] { "id", "actor", "action", "target", "before", "after", "at" },
            ["server_heartbeat"] = new[] { "id", "at" },
            ["server_downtime"] = new[] { "id", "from", "until", "applied_season_id" },
        };

        [Fact]
        public async Task Core_CreatesTheEightTablesWithTheSpecColumns()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            await new MigrationRunner(db.DataSource).ApplyAsync(PgTestDatabase.MigrationsDirectory, CancellationToken.None);
            foreach (KeyValuePair<string, string[]> t in Expected)
            {
                List<string> cols = await db.QueryAsync<string>(
                    "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @T ORDER BY column_name",
                    new { T = t.Key });
                Assert.Equal(t.Value.OrderBy(c => c, StringComparer.Ordinal).ToArray(), cols.ToArray());
            }
        }

        [Fact]
        public async Task Request_ResultIsNullable_FingerprintIsNot_KeyIsAccountAndRequest()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            Assert.Equal("YES", await db.ScalarAsync<string>(
                "SELECT is_nullable FROM information_schema.columns WHERE table_name = 'request' AND column_name = 'result'"));
            Assert.Equal("NO", await db.ScalarAsync<string>(
                "SELECT is_nullable FROM information_schema.columns WHERE table_name = 'request' AND column_name = 'fingerprint'"));
            List<string> pk = await db.QueryAsync<string>(
                "SELECT a.attname FROM pg_index i JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey) " +
                "WHERE i.indrelid = 'request'::regclass AND i.indisprimary ORDER BY a.attname");
            Assert.Equal(new[] { "account_id", "request_id" }, pk.ToArray());
            Assert.Equal("uuid", await db.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_name = 'request' AND column_name = 'request_id'"));
        }

        [Theory]
        [InlineData("UPDATE currency_ledger SET delta = delta")]
        [InlineData("DELETE FROM currency_ledger")]
        [InlineData("TRUNCATE currency_ledger")]
        [InlineData("UPDATE item_ledger SET delta = delta")]
        [InlineData("DELETE FROM item_ledger")]
        [InlineData("TRUNCATE item_ledger")]
        public async Task Ledgers_RejectChanges_EvenFromTheOwner(string sql)
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await WriteOneLineEach(db);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM currency_ledger"));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM item_ledger"));
        }

        [Fact]
        public async Task AppRole_HasExactlySelectAndInsertOnTheLedgers()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            foreach (string table in new[] { "currency_ledger", "item_ledger" })
            {
                List<string> privileges = await db.QueryAsync<string>(
                    "SELECT privilege_type FROM information_schema.role_table_grants WHERE grantee = @R AND table_name = @T ORDER BY privilege_type",
                    new { R = DbRoles.App, T = table });
                Assert.Equal(new[] { "INSERT", "SELECT" }, privileges.ToArray());
            }
        }

        [Fact]
        public async Task AppRole_CannotLogIn()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            Assert.False(await db.ScalarAsync<bool>("SELECT rolcanlogin FROM pg_roles WHERE rolname = @R", new { R = DbRoles.App }));
        }

        [Fact]
        public async Task ItemLedger_RejectsUnknownItemRefShapes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(
                "INSERT INTO item_ledger (account_id, item_ref, delta, reason) VALUES (1, 'gold', 1, 'admin')"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        [Fact]
        public async Task Heartbeat_HasOneRowAtMost()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await db.ExecAsync("INSERT INTO server_heartbeat (at) VALUES (now())");
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync("INSERT INTO server_heartbeat (id, at) VALUES (2, now())"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        private static async Task WriteOneLineEach(PgTestDatabase db)
        {
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            var r = new LedgerRef(LedgerReason.Admin, "test", "1", null);
            await db.Ledger.AddCurrencyAsync(uow, 1, Wizard.Content.CurrencyCode.Gold, Wizard.Content.CurrencyBucket.Earned, 10, false, r);
            await db.Ledger.AddItemAsync(uow, 1, ItemRefs.Stack("item.test"), 1, false, r);
            await uow.CommitAsync(CancellationToken.None);
        }
    }
}
