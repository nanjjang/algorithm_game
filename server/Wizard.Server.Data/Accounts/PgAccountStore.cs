using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Wizard.Server.Data.Accounts
{
    // account 표의 저장소(spec 12 2절, 8.2절). 모든 호출은 호출자의 트랜잭션(IUnitOfWork) 안에서 돈다. 시각은 호출자가 IClock 으로 준다.
    // 이메일은 이 클래스 안에서만 암호화·HMAC 으로 바뀐다: 평문 주소가 SQL 매개변수로 나가는 길이 없다(2.14절).
    // 규칙(입력 검사, 대기, 한도)은 App 의 일이다. 여기는 행을 읽고 쓰는 것만 한다.
    public sealed class PgAccountStore
    {
        // AccountRow 의 열 전부. 열을 더하면 여기와 AccountRow 를 같이 바꾼다
        private const string Columns =
            "id AS Id, login_id AS LoginId, pw_hash AS PwHash, pw_params::text AS PwParams, email_enc AS EmailEnc, email_hmac AS EmailHmac, " +
            "email_verified_at AS EmailVerifiedAt, nickname AS Nickname, created_at AS CreatedAt, last_login AS LastLogin, flags AS Flags, " +
            "sanction_until AS SanctionUntil, play_seconds AS PlaySeconds, league AS League, league_season_id AS LeagueSeasonId, " +
            "show_name_in_alerts AS ShowNameInAlerts, phantom_met AS PhantomMet, revenge_done_at AS RevengeDoneAt, phantom_tries AS PhantomTries, " +
            "phantom_best_dent AS PhantomBestDent, revenge_runs AS RevengeRuns, trade_hold_until AS TradeHoldUntil, " +
            "deletion_requested_at AS DeletionRequestedAt, max_wave_reached AS MaxWaveReached";

        // 계정 번호를 먼저 받는다: 이메일 암호문의 AAD 가 계정 번호라서(2.14절) INSERT 전에 알아야 한다
        private const string NextIdSql = "SELECT nextval(pg_get_serial_sequence('account', 'id'))";

        // 유일성 위반(login_id, lower(nickname))은 ON CONFLICT DO NOTHING 으로 받아 트랜잭션을 살려 둔다.
        // 같은 값의 동시 가입은 유일 색인에서 앞의 트랜잭션이 끝나기를 기다린 뒤 0행이 된다
        private const string InsertSql =
            "INSERT INTO account (id, login_id, pw_hash, pw_params, email_enc, email_hmac, nickname, created_at) OVERRIDING SYSTEM VALUE " +
            "VALUES (@Id, @LoginId, @PwHash, @PwParams::jsonb, @EmailEnc, @EmailHmac, @Nickname, @At) ON CONFLICT DO NOTHING RETURNING id";

        private const string LoginIdExistsSql = "SELECT EXISTS (SELECT 1 FROM account WHERE login_id = @LoginId)";

        private const string NicknameTakenSql = "SELECT EXISTS (SELECT 1 FROM account WHERE lower(nickname) = lower(@Nickname))";

        private const string GetSql = "SELECT " + Columns + " FROM account WHERE id = @Id";

        private const string LockSql = GetSql + " FOR UPDATE";

        private const string GetByLoginIdSql = "SELECT " + Columns + " FROM account WHERE login_id = @LoginId";

        private const string LockByLoginIdSql = GetByLoginIdSql + " FOR UPDATE";

        // 주소 하나에 계정 3개(2.5절)의 셈이 동시 가입·변경에서 같이 지나치지 않도록 그 HMAC 의 트랜잭션 권고 잠금을 먼저 잡는다.
        // 두 int4 꼴의 잠금 공간은 한 bigint 꼴(MigrationRunner)과 겹치지 않는다. objid 가 32비트라 다른 주소끼리 겹치면 잠깐 줄을 설 뿐이다
        private const string LockEmailSql = "SELECT pg_advisory_xact_lock(hashtext('wizard.account.email_hmac'), @Obj)";

        private const string CountByEmailSql = "SELECT count(*) FROM account WHERE email_hmac = @EmailHmac";

        private const string ListByEmailSql =
            "SELECT id AS Id, login_id AS LoginId, email_enc AS EmailEnc, email_verified_at AS EmailVerifiedAt " +
            "FROM account WHERE email_hmac = @EmailHmac ORDER BY id";

        private const string SetPasswordSql = "UPDATE account SET pw_hash = @PwHash, pw_params = @PwParams::jsonb WHERE id = @Id";

        private const string SetEmailSql =
            "UPDATE account SET email_enc = @EmailEnc, email_hmac = @EmailHmac, email_verified_at = @VerifiedAt WHERE id = @Id";

        private const string MarkEmailVerifiedSql = "UPDATE account SET email_verified_at = @At WHERE id = @Id";

        // 2.6절: trade_hold_until = max(trade_hold_until, until)
        private const string ExtendTradeHoldSql =
            "UPDATE account SET trade_hold_until = GREATEST(COALESCE(trade_hold_until, @Until), @Until) WHERE id = @Id RETURNING trade_hold_until";

        private const string SetLastLoginSql = "UPDATE account SET last_login = @At WHERE id = @Id";

        private const string RequestDeletionSql =
            "UPDATE account SET deletion_requested_at = @At WHERE id = @Id AND deletion_requested_at IS NULL";

        private const string CancelDeletionSql =
            "UPDATE account SET deletion_requested_at = NULL WHERE id = @Id AND deletion_requested_at IS NOT NULL";

        private const string ListDeletionDueSql =
            "SELECT id FROM account WHERE deletion_requested_at IS NOT NULL AND deletion_requested_at <= @RequestedAtOrBefore ORDER BY id LIMIT @Limit";

        // 계정 행을 지우면 account_token, session, device, account_settings 가 FK(ON DELETE CASCADE)로 같이 지워진다.
        // 다른 묶음의 표(지팡이, 물건, 친구 …)는 삭제 확정 유스케이스(M10-ACC-04)가 같은 트랜잭션에서 지운다
        private const string DeleteSql = "DELETE FROM account WHERE id = @Id";

        // CONTRACTS 12.4절: 허브가 60초마다, 연결이 끊길 때 더한다
        private const string AddPlaySecondsSql = "UPDATE account SET play_seconds = play_seconds + @Seconds WHERE id = @Id RETURNING play_seconds";

        private const string RaiseMaxWaveSql =
            "UPDATE account SET max_wave_reached = GREATEST(max_wave_reached, @Wave) WHERE id = @Id RETURNING max_wave_reached";

        private const string SetSanctionUntilSql = "UPDATE account SET sanction_until = @Until WHERE id = @Id";

        private const string AddFlagsSql = "UPDATE account SET flags = flags | @Bits WHERE id = @Id RETURNING flags";

        private const string SetShowNameInAlertsSql = "UPDATE account SET show_name_in_alerts = @Show WHERE id = @Id";

        private readonly EmailProtector _email;

        public PgAccountStore(EmailProtector email)
        {
            _email = email ?? throw new ArgumentNullException(nameof(email));
        }

        public EmailProtector Email => _email;

        public async Task<AccountCreateResult> CreateAsync(IUnitOfWork uow, NewAccount account, DateTime at)
        {
            StoreSql.Require(uow);
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }
            long id = await uow.Connection.ExecuteScalarAsync<long>(new CommandDefinition(NextIdSql, null, uow.Transaction)).ConfigureAwait(false);
            byte[] enc = _email.Encrypt(id, account.Email);
            byte[] hmac = _email.LookupHmac(account.Email);
            long? inserted = await uow.Connection.ExecuteScalarAsync<long?>(new CommandDefinition(InsertSql, new
            {
                Id = id,
                account.LoginId,
                account.PwHash,
                PwParams = account.PwParamsJson,
                EmailEnc = enc,
                EmailHmac = hmac,
                account.Nickname,
                At = StoreSql.Utc(at),
            }, uow.Transaction)).ConfigureAwait(false);
            if (inserted != null)
            {
                return new AccountCreateResult(AccountCreateOutcome.Created, inserted.Value);
            }
            if (await LoginIdExistsAsync(uow, account.LoginId).ConfigureAwait(false))
            {
                return new AccountCreateResult(AccountCreateOutcome.LoginIdTaken, 0);
            }
            return new AccountCreateResult(AccountCreateOutcome.NicknameTaken, 0);
        }

        public Task<bool> LoginIdExistsAsync(IUnitOfWork uow, string loginId)
        {
            StoreSql.Require(uow);
            return uow.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(LoginIdExistsSql, new { LoginId = loginId }, uow.Transaction));
        }

        // 대소문자 무시(lower(nickname) 유일 색인과 같은 비교)
        public Task<bool> NicknameTakenAsync(IUnitOfWork uow, string nickname)
        {
            StoreSql.Require(uow);
            return uow.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(NicknameTakenSql, new { Nickname = nickname }, uow.Transaction));
        }

        public Task<AccountRow?> GetAsync(IUnitOfWork uow, long accountId) => StoreSql.One<AccountRow>(uow, GetSql, new { Id = accountId });

        public Task<AccountRow?> LockAsync(IUnitOfWork uow, long accountId) => StoreSql.One<AccountRow>(uow, LockSql, new { Id = accountId });

        public Task<AccountRow?> FindByLoginIdAsync(IUnitOfWork uow, string loginId) => StoreSql.One<AccountRow>(uow, GetByLoginIdSql, new { LoginId = loginId });

        public Task<AccountRow?> LockByLoginIdAsync(IUnitOfWork uow, string loginId) => StoreSql.One<AccountRow>(uow, LockByLoginIdSql, new { LoginId = loginId });

        public string DecryptEmail(AccountRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }
            return _email.Decrypt(row.Id, row.EmailEnc);
        }

        // 2.5절 "주소 하나에 계정 3개": 이 트랜잭션이 끝날 때까지 같은 주소의 다른 가입·변경을 세우고, 지금 묶인 계정 수를 센다
        public async Task<int> LockEmailAndCountAsync(IUnitOfWork uow, string email)
        {
            StoreSql.Require(uow);
            byte[] hmac = _email.LookupHmac(email);
            int obj = BinaryPrimitives.ReadInt32BigEndian(hmac);
            await uow.Connection.ExecuteScalarAsync(new CommandDefinition(LockEmailSql, new { Obj = obj }, uow.Transaction)).ConfigureAwait(false);
            long n = await uow.Connection.ExecuteScalarAsync<long>(new CommandDefinition(CountByEmailSql, new { EmailHmac = hmac }, uow.Transaction))
                .ConfigureAwait(false);
            return checked((int)n);
        }

        // 같은 찾기용 HMAC 의 계정(번호 오름차순). 확인 여부는 호출자가 EmailVerifiedAt 으로 가른다(2.12절: 확인된 계정만)
        public async Task<IReadOnlyList<EmailAccount>> ListByEmailAsync(IUnitOfWork uow, string email)
        {
            StoreSql.Require(uow);
            byte[] hmac = _email.LookupHmac(email);
            IEnumerable<EmailAccount> rows = await uow.Connection.QueryAsync<EmailAccount>(new CommandDefinition(ListByEmailSql, new { EmailHmac = hmac }, uow.Transaction))
                .ConfigureAwait(false);
            return rows.ToList();
        }

        public Task<bool> SetPasswordAsync(IUnitOfWork uow, long accountId, byte[] pwHash, string pwParamsJson)
        {
            if (pwHash == null)
            {
                throw new ArgumentNullException(nameof(pwHash));
            }
            if (pwParamsJson == null)
            {
                throw new ArgumentNullException(nameof(pwParamsJson));
            }
            return StoreSql.Exactly1(uow, SetPasswordSql, new { Id = accountId, PwHash = pwHash, PwParams = pwParamsJson });
        }

        // 주소 바꾸기(2.10절): 새 주소를 이 계정 번호로 암호화하고 찾기용 값을 바꾼다. 옛 암호문은 이 UPDATE 로 사라진다(그 자리에서 지운다)
        public Task<bool> SetEmailAsync(IUnitOfWork uow, long accountId, string email, DateTime? verifiedAt)
        {
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }
            return StoreSql.Exactly1(uow, SetEmailSql, new
            {
                Id = accountId,
                EmailEnc = _email.Encrypt(accountId, email),
                EmailHmac = _email.LookupHmac(email),
                VerifiedAt = StoreSql.UtcOrNull(verifiedAt),
            });
        }

        public Task<bool> MarkEmailVerifiedAsync(IUnitOfWork uow, long accountId, DateTime at) =>
            StoreSql.Exactly1(uow, MarkEmailVerifiedSql, new { Id = accountId, At = StoreSql.Utc(at) });

        // 바뀐 뒤의 trade_hold_until. 계정이 없으면 null
        public Task<DateTime?> ExtendTradeHoldAsync(IUnitOfWork uow, long accountId, DateTime until)
        {
            StoreSql.Require(uow);
            return uow.Connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(ExtendTradeHoldSql, new { Id = accountId, Until = StoreSql.Utc(until) }, uow.Transaction));
        }

        public Task<bool> SetLastLoginAsync(IUnitOfWork uow, long accountId, DateTime at) =>
            StoreSql.Exactly1(uow, SetLastLoginSql, new { Id = accountId, At = StoreSql.Utc(at) });

        // 이미 요청돼 있으면 false(처음 요청 시각을 바꾸지 않는다)
        public Task<bool> RequestDeletionAsync(IUnitOfWork uow, long accountId, DateTime at) =>
            StoreSql.Exactly1(uow, RequestDeletionSql, new { Id = accountId, At = StoreSql.Utc(at) });

        public Task<bool> CancelDeletionAsync(IUnitOfWork uow, long accountId) => StoreSql.Exactly1(uow, CancelDeletionSql, new { Id = accountId });

        // 삭제 요청 시각이 requestedAtOrBefore 이하인 계정(번호 오름차순, 최대 limit 개)
        public async Task<IReadOnlyList<long>> ListDeletionDueAsync(IUnitOfWork uow, DateTime requestedAtOrBefore, int limit)
        {
            StoreSql.Require(uow);
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
            IEnumerable<long> ids = await uow.Connection.QueryAsync<long>(new CommandDefinition(ListDeletionDueSql,
                new { RequestedAtOrBefore = StoreSql.Utc(requestedAtOrBefore), Limit = limit }, uow.Transaction)).ConfigureAwait(false);
            return ids.ToList();
        }

        public Task<bool> DeleteAsync(IUnitOfWork uow, long accountId) => StoreSql.Exactly1(uow, DeleteSql, new { Id = accountId });

        // 바뀐 뒤의 play_seconds. 계정이 없으면 null
        public Task<long?> AddPlaySecondsAsync(IUnitOfWork uow, long accountId, long seconds)
        {
            StoreSql.Require(uow);
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "play time only grows");
            }
            return uow.Connection.ExecuteScalarAsync<long?>(new CommandDefinition(AddPlaySecondsSql, new { Id = accountId, Seconds = seconds }, uow.Transaction));
        }

        // 바뀐 뒤의 max_wave_reached(줄지 않는다). 계정이 없으면 null
        public Task<int?> RaiseMaxWaveAsync(IUnitOfWork uow, long accountId, int wave)
        {
            StoreSql.Require(uow);
            return uow.Connection.ExecuteScalarAsync<int?>(new CommandDefinition(RaiseMaxWaveSql, new { Id = accountId, Wave = wave }, uow.Transaction));
        }

        public Task<bool> SetSanctionUntilAsync(IUnitOfWork uow, long accountId, DateTime? until) =>
            StoreSql.Exactly1(uow, SetSanctionUntilSql, new { Id = accountId, Until = StoreSql.UtcOrNull(until) });

        // 바뀐 뒤의 flags. 계정이 없으면 null
        public Task<long?> AddFlagsAsync(IUnitOfWork uow, long accountId, long bits)
        {
            StoreSql.Require(uow);
            return uow.Connection.ExecuteScalarAsync<long?>(new CommandDefinition(AddFlagsSql, new { Id = accountId, Bits = bits }, uow.Transaction));
        }

        public Task<bool> SetShowNameInAlertsAsync(IUnitOfWork uow, long accountId, bool show) =>
            StoreSql.Exactly1(uow, SetShowNameInAlertsSql, new { Id = accountId, Show = show });
    }
}
