using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // account_token 표의 저장소(spec 12 2.9~2.11절, 8.2절). 코드의 생성·해시·비교·한도는 App 이 하고 여기는 행만 다룬다.
    // 틀린 횟수의 상한(5)과 수명(15분, 30분)은 규칙 값이라 호출자가 넘긴다.
    public sealed class PgAccountTokenStore
    {
        private const string Columns =
            "id AS Id, account_id AS AccountId, kind AS Kind, token_hash AS TokenHash, new_email_enc AS NewEmailEnc, attempts AS Attempts, " +
            "expires_at AS ExpiresAt, used_at AS UsedAt, created_at AS CreatedAt";

        // IP 는 inet 이다. Dapper 가 IPAddress 매개변수를 모르므로 문자열로 넘겨 SQL 에서 바꾼다
        private const string InsertSql =
            "INSERT INTO account_token (account_id, kind, token_hash, new_email_enc, attempts, expires_at, used_at, created_ip, created_at) " +
            "VALUES (@AccountId, @Kind, @TokenHash, @NewEmailEnc, 0, @ExpiresAt, NULL, @CreatedIp::inet, @At) RETURNING id";

        // "새 코드를 보내면 앞의 미사용 코드는 폐기"(2.9절 [임시], 2.11절)
        private const string RevokeActiveSql =
            "UPDATE account_token SET used_at = @At WHERE account_id = @AccountId AND kind = @Kind AND used_at IS NULL";

        // 2.9절 VerifyEmailCode, 2.11절 ConfirmPasswordReset: 그 계정·종류의 살아 있는 가장 새 코드를 잠근다
        private const string LockLatestActiveSql =
            "SELECT " + Columns + " FROM account_token WHERE account_id = @AccountId AND kind = @Kind AND used_at IS NULL AND expires_at > @Now " +
            "ORDER BY created_at DESC, id DESC LIMIT 1 FOR UPDATE";

        private const string GetSql = "SELECT " + Columns + " FROM account_token WHERE id = @Id";

        // 틀림 1회. 상한에 닿으면 그 자리에서 폐기한다. 이미 쓴(폐기된) 코드는 바꾸지 않는다
        private const string RecordWrongSql =
            "UPDATE account_token SET attempts = attempts + 1, " +
            "used_at = CASE WHEN attempts + 1 >= @DiscardAt THEN @At ELSE used_at END " +
            "WHERE id = @Id AND used_at IS NULL RETURNING attempts";

        private const string MarkUsedSql = "UPDATE account_token SET used_at = @At WHERE id = @Id AND used_at IS NULL";

        // 다시 보내기 한도(2.9절 60초 1회·하루 5회)를 셀 때
        private const string CountCreatedSinceSql =
            "SELECT count(*) FROM account_token WHERE account_id = @AccountId AND kind = @Kind AND created_at > @Since";

        // 8.4절: 쓰거나 만료된 코드는 24시간 뒤 지운다. cutoff = now − 24h
        private const string DeleteSettledSql =
            "DELETE FROM account_token WHERE (used_at IS NOT NULL AND used_at < @Cutoff) OR expires_at < @Cutoff";

        // online 10: IP 는 30일 뒤 지운다 [임시: NULL 로 덮음]. cutoff = now − 30일
        private const string ClearIpSql = "UPDATE account_token SET created_ip = NULL WHERE created_ip IS NOT NULL AND created_at < @Cutoff";

        private readonly EmailProtector _email;

        public PgAccountTokenStore(EmailProtector email)
        {
            _email = email ?? throw new ArgumentNullException(nameof(email));
        }

        public Task<long> InsertAsync(IUnitOfWork uow, NewAccountToken token, DateTime at)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            return StoreSql.Scalar<long>(uow, InsertSql, new
            {
                token.AccountId,
                Kind = AccountDbNames.TokenKind(token.Kind),
                token.TokenHash,
                NewEmailEnc = token.NewEmail == null ? null : _email.Encrypt(token.AccountId, token.NewEmail),
                ExpiresAt = StoreSql.Utc(token.ExpiresAt),
                CreatedIp = token.CreatedIp?.ToString(),
                At = StoreSql.Utc(at),
            });
        }

        // 폐기한 행 수
        public Task<int> RevokeActiveAsync(IUnitOfWork uow, long accountId, AccountTokenKind kind, DateTime at) =>
            StoreSql.Execute(uow, RevokeActiveSql, new { AccountId = accountId, Kind = AccountDbNames.TokenKind(kind), At = StoreSql.Utc(at) });

        public Task<AccountTokenRow?> LockLatestActiveAsync(IUnitOfWork uow, long accountId, AccountTokenKind kind, DateTime now) =>
            StoreSql.One<AccountTokenRow>(uow, LockLatestActiveSql, new { AccountId = accountId, Kind = AccountDbNames.TokenKind(kind), Now = StoreSql.Utc(now) });

        public Task<AccountTokenRow?> GetAsync(IUnitOfWork uow, long tokenId) => StoreSql.One<AccountTokenRow>(uow, GetSql, new { Id = tokenId });

        // 바뀐 뒤의 틀린 횟수. discardAtAttempts 에 닿으면 used_at 이 채워진다(폐기). 이미 쓴 코드면 null
        public async Task<short?> RecordWrongAttemptAsync(IUnitOfWork uow, long tokenId, short discardAtAttempts, DateTime at)
        {
            if (discardAtAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(discardAtAttempts));
            }
            IReadOnlyList<short> after = await StoreSql.Many<short>(uow, RecordWrongSql, new { Id = tokenId, DiscardAt = discardAtAttempts, At = StoreSql.Utc(at) })
                .ConfigureAwait(false);
            return after.Count == 0 ? (short?)null : after[0];
        }

        // 한 번만: 아직 쓰지 않은 코드일 때만 true
        public Task<bool> MarkUsedAsync(IUnitOfWork uow, long tokenId, DateTime at) =>
            StoreSql.Exactly1(uow, MarkUsedSql, new { Id = tokenId, At = StoreSql.Utc(at) });

        public Task<int> CountCreatedSinceAsync(IUnitOfWork uow, long accountId, AccountTokenKind kind, DateTime since) =>
            StoreSql.Count(uow, CountCreatedSinceSql, new { AccountId = accountId, Kind = AccountDbNames.TokenKind(kind), Since = StoreSql.Utc(since) });

        // change_email 코드의 새 주소(2.10절). 다른 계정의 행으로 옮겨진 암호문이면 CryptographicException
        public string? DecryptNewEmail(AccountTokenRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }
            return row.NewEmailEnc == null ? null : _email.Decrypt(row.AccountId, row.NewEmailEnc);
        }

        public Task<int> DeleteSettledBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteSettledSql, new { Cutoff = StoreSql.Utc(cutoff) });

        public Task<int> ClearCreatedIpBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, ClearIpSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
