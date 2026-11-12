using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // M10-ACC-01 수락 2: 이메일은 암호화한 값과 HMAC 만 저장한다. 평문 이메일과 평문 비밀번호가 든 열이 없다(spec 12 2.14절)
    public sealed class PlaintextStorageTests
    {
        private static readonly string[] Tables =
        {
            "account", "account_token", "mail_outbox", "session", "session_refresh_used", "device", "login_attempt", "signup_log",
            "recovery_request_log", "account_settings", "sanction",
        };

        // 글자 열(text, jsonb)은 이것뿐이다. 어느 것도 주소·비밀번호·코드·토큰을 담지 않는다(열거값, 아이디, 닉네임, 해시 매개변수, 사유)
        private static readonly string[] TextColumns =
        {
            "account.login_id", "account.nickname", "account.pw_params",
            "account_settings.settings",
            "account_token.kind",
            "login_attempt.login_id",
            "mail_outbox.kind", "mail_outbox.provider_ref", "mail_outbox.status",
            "recovery_request_log.kind",
            "sanction.by", "sanction.kind", "sanction.reason",
        };

        [Fact]
        public async Task OnlyTheseTextColumnsExist_AndEverySecretColumnIsBytes()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            List<string> text = await db.QueryAsync<string>(
                "SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = ANY(@T) " +
                "AND data_type IN ('text', 'character varying', 'character', 'json', 'jsonb', 'xml', 'ARRAY', 'USER-DEFINED') ORDER BY 1",
                new { T = Tables });
            Assert.Equal(TextColumns.OrderBy(c => c, StringComparer.Ordinal).ToArray(), text.ToArray());

            // 이름에 email, pw, password, token, hash 가 든 열은 바이트(암호문·HMAC·해시)이거나 그 시각·번호·종류다
            List<ColumnType> secretish = await db.QueryAsync<ColumnType>(
                "SELECT table_name || '.' || column_name AS Name, data_type AS Type FROM information_schema.columns " +
                "WHERE table_schema = 'public' AND table_name = ANY(@T) AND column_name ~ '(email|pw|password|passwd|token|hash|secret|code)' ORDER BY 1",
                new { T = Tables });
            var allowedNonBytes = new HashSet<string>(StringComparer.Ordinal)
            {
                "account.email_verified_at", "account.pw_params", "mail_outbox.token_id",
            };
            foreach (ColumnType c in secretish)
            {
                Assert.True(c.Type == "bytea" || allowedNonBytes.Contains(c.Name), c.Name + " is " + c.Type);
            }
            Assert.Contains(secretish, c => c.Name == "account.email_enc" && c.Type == "bytea");
            Assert.Contains(secretish, c => c.Name == "account.email_hmac" && c.Type == "bytea");
            Assert.Contains(secretish, c => c.Name == "account.pw_hash" && c.Type == "bytea");
            Assert.Contains(secretish, c => c.Name == "account_token.new_email_enc" && c.Type == "bytea");
            Assert.Contains(secretish, c => c.Name == "account_token.token_hash" && c.Type == "bytea");
            Assert.DoesNotContain(secretish, c => c.Name.EndsWith(".email", StringComparison.Ordinal) || c.Name.EndsWith(".password", StringComparison.Ordinal));
        }

        // 가입부터 주소 바꾸기까지 저장소를 실제로 돌린 뒤 열한 표의 모든 행을 글자로 떠서 평문 주소가 어디에도 없음을 본다(대소문자, UTF-8 바이트의 16진 꼴 포함)
        [Fact]
        public async Task AfterSignupAndEmailChange_NoRowContainsAnAddressInAnyForm()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            EmailProtector protector = AccountsKit.NewProtector();
            var accounts = new PgAccountStore(protector);
            var tokens = new PgAccountTokenStore(protector);
            const string first = "Wizard.Student@Example.com";
            const string second = "new.address@mail.example.co.kr";
            DateTime now = db.Clock.UtcNow;

            long id = await AccountsKit.CreateAsync(db, accounts, "student1", "Apprentice", first);
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                await tokens.InsertAsync(uow, new NewAccountToken(id, AccountTokenKind.VerifyEmail, AccountsKit.Hash("123456"), now.AddMinutes(15), IPAddress.Loopback, null), now);
                await tokens.InsertAsync(uow, new NewAccountToken(id, AccountTokenKind.ChangeEmail, AccountsKit.Hash("654321"), now.AddMinutes(15), IPAddress.Loopback, second), now);
                await new PgMailOutboxStore().EnqueueAsync(uow, MailOutboxKind.VerifyEmail, id, null, now);
                await new PgSignupLogStore().InsertAsync(uow, IPAddress.Loopback, AccountsKit.Hash("device"), now);
                await new PgLoginAttemptStore().InsertAsync(uow, "student1", IPAddress.Loopback, now, true);
                await new PgRecoveryRequestLogStore().InsertAsync(uow, id, IPAddress.Loopback, RecoveryRequestKind.ResetPassword, now);
                await new PgAccountSettingsStore().PutAsync(uow, id, "{\"volume\":5}", now);
                await uow.CommitAsync(AccountsKit.None);
            }

            await AssertNowhere(db, first);
            await AssertNowhere(db, second);

            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                Assert.True(await accounts.SetEmailAsync(uow, id, second, now));
                await uow.CommitAsync(AccountsKit.None);
            }
            await AssertNowhere(db, first);
            await AssertNowhere(db, second);

            // 저장된 것은 풀면 그 주소다(암호화가 실제로 된다)
            await using (IUnitOfWork uow = await db.App.BeginAsync(AccountsKit.None))
            {
                AccountRow row = (await accounts.GetAsync(uow, id))!;
                Assert.Equal(second, accounts.DecryptEmail(row));
                Assert.Equal(protector.LookupHmac(second), row.EmailHmac);
            }
        }

        // 저장소의 공개 입력에 비밀번호 원문 자리가 없다: 비밀번호는 해시(byte[])와 매개변수만 받는다
        [Fact]
        public void StoreInputs_HaveNoPlaintextPasswordParameter()
        {
            Assembly data = typeof(PgAccountStore).Assembly;
            foreach (Type t in data.GetExportedTypes().Where(t => t.Namespace == "Wizard.Server.Data.Accounts"))
            {
                foreach (MethodBase m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                             .Cast<MethodBase>().Concat(t.GetConstructors()))
                {
                    foreach (ParameterInfo p in m.GetParameters())
                    {
                        string n = p.Name ?? "";
                        Assert.False(n.Contains("password", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(string),
                            t.Name + "." + m.Name + "(" + n + ") takes a plaintext password");
                    }
                }
                foreach (PropertyInfo p in t.GetProperties())
                {
                    Assert.False(p.Name.Contains("Password", StringComparison.Ordinal) && p.PropertyType == typeof(string), t.Name + "." + p.Name);
                }
            }
        }

        private static async Task AssertNowhere(PgTestDatabase db, string email)
        {
            string[] forms =
            {
                email, email.ToLowerInvariant(), email.ToUpperInvariant(),
                Hex(email), Hex(email.ToLowerInvariant()),
            };
            foreach (string table in Tables)
            {
                // 표 이름은 위 고정 목록뿐이다(식별자는 매개변수가 될 수 없다)
                List<string> rows = await db.QueryAsync<string>("SELECT t::text FROM " + table + " t");
                foreach (string row in rows)
                {
                    foreach (string f in forms)
                    {
                        Assert.False(row.Contains(f, StringComparison.OrdinalIgnoreCase), table + " row holds the address in plaintext (" + f + ")");
                    }
                }
            }
        }

        private static string Hex(string s) => Convert.ToHexString(Encoding.UTF8.GetBytes(s)).ToLowerInvariant();

        private sealed class ColumnType
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
        }
    }
}
