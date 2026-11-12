using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Wizard.Server.Data;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // M10-ACC-01 수락 1: 0010_accounts.sql 의 열한 표가 CONTRACTS 12.2절·spec 12 8.2절과 같다(열 이름, 형식, NULL 여부, 제약, 권한)
    public sealed class AccountsSchemaTests
    {
        private const string B = "bigint";
        private const string T = "text";
        private const string Y = "bytea";
        private const string J = "jsonb";
        private const string Ts = "timestamp with time zone";
        private const string Bool = "boolean";
        private const string S = "smallint";
        private const string I = "integer";
        private const string U = "uuid";
        private const string Ip = "inet";

        // 표 → (열, 형식, NULL 허용). 기대값은 spec 12 8.2절 DDL 을 그대로 옮긴 것이다(account.play_seconds 는 CONTRACTS 12.1절도 같다).
        // account_settings 는 CONTRACTS 12.2절: account_id, settings jsonb, updated_at
        private static readonly Dictionary<string, (string Name, string Type, bool Nullable)[]> Expected = new Dictionary<string, (string, string, bool)[]>
        {
            ["account"] = new[]
            {
                ("id", B, false), ("login_id", T, false), ("pw_hash", Y, false), ("pw_params", J, false), ("email_enc", Y, false),
                ("email_hmac", Y, false), ("email_verified_at", Ts, true), ("nickname", T, false), ("created_at", Ts, false),
                ("last_login", Ts, true), ("flags", B, false), ("sanction_until", Ts, true), ("play_seconds", B, false), ("league", S, false),
                ("league_season_id", B, true), ("show_name_in_alerts", Bool, false), ("phantom_met", Bool, false), ("revenge_done_at", Ts, true),
                ("phantom_tries", I, false), ("phantom_best_dent", I, false), ("revenge_runs", I, false), ("trade_hold_until", Ts, true),
                ("deletion_requested_at", Ts, true), ("max_wave_reached", I, false),
            },
            ["account_token"] = new[]
            {
                ("id", B, false), ("account_id", B, false), ("kind", T, false), ("token_hash", Y, false), ("new_email_enc", Y, true),
                ("attempts", S, false), ("expires_at", Ts, false), ("used_at", Ts, true), ("created_ip", Ip, true), ("created_at", Ts, false),
            },
            ["mail_outbox"] = new[]
            {
                ("id", B, false), ("kind", T, false), ("account_id", B, true), ("status", T, false), ("tries", S, false), ("provider_ref", T, true),
                ("created_at", Ts, false), ("sent_at", Ts, true), ("next_try_at", Ts, true), ("token_id", B, true),
            },
            ["session"] = new[]
            {
                ("id", B, false), ("account_id", B, false), ("device_id", B, false), ("access_hash", Y, false), ("refresh_hash", Y, false),
                ("family_id", U, false), ("expires_at", Ts, false), ("access_expires_at", Ts, false), ("revoked_at", Ts, true),
            },
            ["session_refresh_used"] = new[]
            {
                ("refresh_hash", Y, false), ("family_id", U, false), ("account_id", B, false), ("device_id", B, false), ("at", Ts, false),
            },
            ["device"] = new[]
            {
                ("id", B, false), ("account_id", B, false), ("device_hash", Y, false), ("first_seen", Ts, false), ("last_seen", Ts, false),
            },
            ["login_attempt"] = new[] { ("login_id", T, false), ("ip", Ip, false), ("at", Ts, false), ("ok", Bool, false) },
            ["signup_log"] = new[] { ("ip", Ip, false), ("device_hash", Y, false), ("at", Ts, false) },
            ["recovery_request_log"] = new[] { ("account_id", B, true), ("ip", Ip, false), ("kind", T, false), ("at", Ts, false) },
            ["account_settings"] = new[] { ("account_id", B, false), ("settings", J, false), ("updated_at", Ts, false) },
            ["sanction"] = new[]
            {
                ("id", B, false), ("account_id", B, false), ("kind", T, false), ("reason", T, false), ("from", Ts, false), ("until", Ts, true),
                ("by", T, false),
            },
        };

        // 표마다 wizard_app 의 권한. 원장처럼 고치면 안 되는 기록(쓴 갱신 토큰, 로그인·가입·복구 기록)에는 UPDATE 가 없다. 제재 이력은 지우지 않는다
        private static readonly Dictionary<string, string[]> Grants = new Dictionary<string, string[]>
        {
            ["account"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["account_token"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["mail_outbox"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["session"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["session_refresh_used"] = new[] { "DELETE", "INSERT", "SELECT" },
            ["device"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["login_attempt"] = new[] { "DELETE", "INSERT", "SELECT" },
            ["signup_log"] = new[] { "DELETE", "INSERT", "SELECT" },
            ["recovery_request_log"] = new[] { "DELETE", "INSERT", "SELECT" },
            ["account_settings"] = new[] { "DELETE", "INSERT", "SELECT", "UPDATE" },
            ["sanction"] = new[] { "INSERT", "SELECT", "UPDATE" },
        };

        [Fact]
        public async Task TheElevenTables_HaveExactlyTheSpecColumns_TypesAndNullability()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            await new MigrationRunner(db.DataSource).ApplyAsync(PgTestDatabase.MigrationsDirectory, CancellationToken.None);
            Assert.Equal(11, Expected.Count);
            foreach (KeyValuePair<string, (string Name, string Type, bool Nullable)[]> t in Expected)
            {
                List<ColumnInfo> cols = await db.QueryAsync<ColumnInfo>(
                    "SELECT column_name AS Name, data_type AS Type, is_nullable = 'YES' AS Nullable FROM information_schema.columns " +
                    "WHERE table_schema = 'public' AND table_name = @T ORDER BY column_name", new { T = t.Key });
                string[] want = t.Value.OrderBy(c => c.Name, StringComparer.Ordinal).Select(c => c.Name + ":" + c.Type + ":" + (c.Nullable ? "null" : "not null")).ToArray();
                string[] got = cols.Select(c => c.Name + ":" + c.Type + ":" + (c.Nullable ? "null" : "not null")).ToArray();
                Assert.True(want.SequenceEqual(got), t.Key + "\n want: " + string.Join(", ", want) + "\n  got: " + string.Join(", ", got));
            }
        }

        [Fact]
        public async Task Keys_UniqueIndexes_AndForeignKeys_AreTheSpecOnes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            Assert.Equal(new[] { "id" }, await PrimaryKey(db, "account"));
            Assert.Equal(new[] { "refresh_hash" }, await PrimaryKey(db, "session_refresh_used"));
            Assert.Equal(new[] { "account_id" }, await PrimaryKey(db, "account_settings"));
            Assert.Empty(await PrimaryKey(db, "login_attempt"));
            Assert.Empty(await PrimaryKey(db, "signup_log"));
            Assert.Empty(await PrimaryKey(db, "recovery_request_log"));

            List<string> unique = await db.QueryAsync<string>(
                "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename IN ('account','session','device') AND indexdef LIKE 'CREATE UNIQUE%' ORDER BY indexdef");
            Assert.Contains(unique, d => d.Contains("(login_id)", StringComparison.Ordinal));
            Assert.Contains(unique, d => d.Contains("lower(nickname)", StringComparison.Ordinal));
            Assert.Contains(unique, d => d.Contains("(access_hash)", StringComparison.Ordinal));
            Assert.Contains(unique, d => d.Contains("(refresh_hash)", StringComparison.Ordinal));
            Assert.Contains(unique, d => d.Contains("(account_id, device_hash)", StringComparison.Ordinal));
            // email_hmac 은 유일하지 않다(주소 하나에 3행까지)
            Assert.DoesNotContain(unique, d => d.Contains("email_hmac", StringComparison.Ordinal));

            // FK: 계정을 지우면 따라 지워지는 표는 넷(spec 12 8.2절 + account_settings). 원장처럼 계정 뒤에 남는 기록에는 FK 가 없다
            List<string> fks = await db.QueryAsync<string>(
                "SELECT c.conrelid::regclass::text || '->' || c.confrelid::regclass::text || ':' || c.confdeltype::text FROM pg_constraint c " +
                "WHERE c.contype = 'f' AND c.confrelid = 'account'::regclass ORDER BY 1");
            Assert.Equal(new[] { "account_settings->account:c", "account_token->account:c", "device->account:c", "session->account:c" }, fks.ToArray());
        }

        [Fact]
        public async Task AppRole_HasExactlyTheIntendedPrivileges()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            foreach (KeyValuePair<string, string[]> g in Grants)
            {
                List<string> privileges = await db.QueryAsync<string>(
                    "SELECT privilege_type FROM information_schema.role_table_grants WHERE grantee = @R AND table_name = @T ORDER BY privilege_type",
                    new { R = DbRoles.App, T = g.Key });
                Assert.True(g.Value.SequenceEqual(privileges), g.Key + ": " + string.Join(",", privileges));
            }
            // 가입이 nextval 로 번호를 먼저 받는다(PgAccountStore.CreateAsync). 다른 시퀀스 권한은 주지 않는다
            Assert.True(await db.ScalarAsync<bool>("SELECT has_sequence_privilege(@R, 'account_id_seq', 'USAGE')", new { R = DbRoles.App }));
            Assert.False(await db.ScalarAsync<bool>("SELECT has_sequence_privilege(@R, 'account_id_seq', 'UPDATE')", new { R = DbRoles.App }));
        }

        [Theory]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('Abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1')")]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abc', '\\x00', '{}', '\\x00', '\\x00', 'n1')")]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abcdefghijklmnopq', '\\x00', '{}', '\\x00', '\\x00', 'n1')")]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abc_d', '\\x00', '{}', '\\x00', '\\x00', 'n1')")]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname, league) VALUES ('abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1', 3)")]
        [InlineData("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname, play_seconds) VALUES ('abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1', -1)")]
        [InlineData("INSERT INTO mail_outbox (kind, status) VALUES ('verify_email', 'delivered')")]
        [InlineData("INSERT INTO mail_outbox (kind, status) VALUES ('newsletter', 'queued')")]
        [InlineData("INSERT INTO recovery_request_log (ip, kind, at) VALUES ('127.0.0.1', 'verify_email', now())")]
        [InlineData("INSERT INTO sanction (account_id, kind, reason, \"from\", by) VALUES (1, 'market', 'r', now(), 'system')")]
        public async Task Checks_RejectValuesOutsideTheSpec(string sql)
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(sql));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        [Theory]
        [InlineData("change_email", 6)]
        [InlineData("reset_password", -1)]
        [InlineData("unknown", 0)]
        public async Task AccountToken_KindAndAttempts_AreChecked(string kind, int attempts)
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long id = await db.ScalarAsync<long>(
                "INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1') RETURNING id");
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(
                "INSERT INTO account_token (account_id, kind, token_hash, attempts, expires_at) VALUES (@Id, @Kind, '\\x01', @A, now())",
                new { Id = id, Kind = kind, A = (short)attempts }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        [Fact]
        public async Task Account_Defaults_AreTheSpecOnes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            await db.ExecAsync("INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1')");
            List<DefaultsRow> rows = await db.QueryAsync<DefaultsRow>(
                "SELECT flags AS Flags, play_seconds AS PlaySeconds, league AS League, show_name_in_alerts AS ShowNameInAlerts, phantom_met AS PhantomMet, " +
                "phantom_tries AS PhantomTries, phantom_best_dent AS PhantomBestDent, revenge_runs AS RevengeRuns, max_wave_reached AS MaxWaveReached, " +
                "email_verified_at IS NULL AS Unverified, trade_hold_until IS NULL AS NoHold, deletion_requested_at IS NULL AS NotDeleting FROM account");
            DefaultsRow r = Assert.Single(rows);
            Assert.Equal(0L, r.Flags);
            Assert.Equal(0L, r.PlaySeconds);
            Assert.Equal((short)0, r.League);              // 새싹
            Assert.True(r.ShowNameInAlerts);               // 기본값 [임시]
            Assert.False(r.PhantomMet);
            Assert.Equal(0, r.PhantomTries);
            Assert.Equal(0, r.PhantomBestDent);
            Assert.Equal(0, r.RevengeRuns);
            Assert.Equal(0, r.MaxWaveReached);
            Assert.True(r.Unverified);
            Assert.True(r.NoHold);
            Assert.True(r.NotDeleting);
        }

        [Fact]
        public async Task LoginId_IsUnique_Nickname_IsUniqueIgnoringCase_EmailHmac_IsNot()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            const string insert = "INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES (@L, '\\x00', '{}', '\\x00', '\\xAA', @N)";
            await db.ExecAsync(insert, new { L = "alpha", N = "Mage" });
            await db.ExecAsync(insert, new { L = "bravo", N = "Mage2" });
            await db.ExecAsync(insert, new { L = "charlie", N = "Mage3" });   // 같은 email_hmac 세 줄
            PostgresException dupId = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(insert, new { L = "alpha", N = "Other" }));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, dupId.SqlState);
            PostgresException dupNick = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync(insert, new { L = "delta", N = "MAGE" }));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, dupNick.SqlState);
            Assert.Equal("account_nickname_ci", dupNick.ConstraintName);
        }

        [Fact]
        public async Task DeletingAnAccount_CascadesToItsTokensSessionsDevicesAndSettings_AndKeepsTheRecords()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            long id = await db.ScalarAsync<long>(
                "INSERT INTO account (login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) VALUES ('abcd', '\\x00', '{}', '\\x00', '\\x00', 'n1') RETURNING id");
            await db.ExecAsync("INSERT INTO account_token (account_id, kind, token_hash, expires_at) VALUES (@Id, 'verify_email', '\\x01', now())", new { Id = id });
            long device = await db.ScalarAsync<long>(
                "INSERT INTO device (account_id, device_hash, first_seen, last_seen) VALUES (@Id, '\\x02', now(), now()) RETURNING id", new { Id = id });
            await db.ExecAsync(
                "INSERT INTO session (account_id, device_id, access_hash, refresh_hash, family_id, expires_at, access_expires_at) " +
                "VALUES (@Id, @D, '\\x03', '\\x04', gen_random_uuid(), now(), now())", new { Id = id, D = device });
            await db.ExecAsync("INSERT INTO account_settings (account_id, settings, updated_at) VALUES (@Id, '{}', now())", new { Id = id });
            await db.ExecAsync("INSERT INTO mail_outbox (kind, account_id, status) VALUES ('verify_email', @Id, 'sent')", new { Id = id });
            await db.ExecAsync("INSERT INTO sanction (account_id, kind, reason, \"from\", by) VALUES (@Id, 'chat', 'r', now(), 'system')", new { Id = id });
            await db.ExecAsync("INSERT INTO login_attempt (login_id, ip, at, ok) VALUES ('abcd', '127.0.0.1', now(), true)");

            await db.ExecAsync("DELETE FROM account WHERE id = @Id", new { Id = id });

            foreach (string gone in new[] { "account_token", "device", "session", "account_settings" })
            {
                Assert.Equal(0L, await db.ScalarAsync<long>("SELECT count(*) FROM " + gone));
            }
            foreach (string kept in new[] { "mail_outbox", "sanction", "login_attempt" })
            {
                Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM " + kept));
            }
        }

        private static async Task<string[]> PrimaryKey(PgTestDatabase db, string table) =>
            (await db.QueryAsync<string>(
                "SELECT a.attname FROM pg_index i JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey) " +
                "WHERE i.indrelid = @T::regclass AND i.indisprimary ORDER BY a.attname", new { T = table })).ToArray();

        private sealed class ColumnInfo
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public bool Nullable { get; set; }
        }

        private sealed class DefaultsRow
        {
            public long Flags { get; set; }
            public long PlaySeconds { get; set; }
            public short League { get; set; }
            public bool ShowNameInAlerts { get; set; }
            public bool PhantomMet { get; set; }
            public int PhantomTries { get; set; }
            public int PhantomBestDent { get; set; }
            public int RevengeRuns { get; set; }
            public int MaxWaveReached { get; set; }
            public bool Unverified { get; set; }
            public bool NoHold { get; set; }
            public bool NotDeleting { get; set; }
        }
    }
}
