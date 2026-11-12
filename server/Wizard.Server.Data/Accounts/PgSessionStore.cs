using System;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // session, session_refresh_used 표의 저장소(spec 12 2.3절 세션 상태 기계). 토큰 생성·해시·수명 계산은 App 이 한다.
    //   갱신: LockByRefreshHashAsync(FOR UPDATE) → 없으면 FindRefreshUsedAsync 로 재사용 탐지 → 있으면 RotateAsync.
    //   같은 갱신 토큰의 동시 갱신 둘: 뒤의 것은 앞의 트랜잭션이 끝날 때까지 기다린 뒤 해시가 바뀐 행을 다시 보므로 null 을 받고,
    //   물러난 해시는 session_refresh_used 에 있으므로 재사용으로 드러난다.
    public sealed class PgSessionStore
    {
        private const string Columns =
            "id AS Id, account_id AS AccountId, device_id AS DeviceId, access_hash AS AccessHash, refresh_hash AS RefreshHash, " +
            "family_id AS FamilyId, expires_at AS ExpiresAt, access_expires_at AS AccessExpiresAt, revoked_at AS RevokedAt";

        private const string InsertSql =
            "INSERT INTO session (account_id, device_id, access_hash, refresh_hash, family_id, expires_at, access_expires_at, revoked_at) " +
            "VALUES (@AccountId, @DeviceId, @AccessHash, @RefreshHash, @FamilyId, @RefreshExpiresAt, @AccessExpiresAt, NULL) RETURNING id";

        // 접근 토큰 확인: 폐기되지 않았고 접근 만료 전인 세션만
        private const string FindActiveByAccessSql =
            "SELECT " + Columns + " FROM session WHERE access_hash = @AccessHash AND revoked_at IS NULL AND access_expires_at > @Now";

        private const string LockByRefreshSql = "SELECT " + Columns + " FROM session WHERE refresh_hash = @RefreshHash FOR UPDATE";

        private const string GetSql = "SELECT " + Columns + " FROM session WHERE id = @Id";

        private const string FindRefreshUsedSql =
            "SELECT refresh_hash AS RefreshHash, family_id AS FamilyId, account_id AS AccountId, device_id AS DeviceId, at AS At " +
            "FROM session_refresh_used WHERE refresh_hash = @RefreshHash";

        private const string InsertRefreshUsedSql =
            "INSERT INTO session_refresh_used (refresh_hash, family_id, account_id, device_id, at) " +
            "SELECT refresh_hash, family_id, account_id, device_id, @At FROM session WHERE id = @Id";

        // 회전(2.3절): 살아 있는 세션만. 갱신 만료도 새로 잡는다(슬라이딩 [임시])
        private const string RotateSql =
            "UPDATE session SET access_hash = @AccessHash, access_expires_at = @AccessExpiresAt, " +
            "refresh_hash = @RefreshHash, expires_at = @RefreshExpiresAt " +
            "WHERE id = @Id AND revoked_at IS NULL";

        private const string RevokeOneSql = "UPDATE session SET revoked_at = @At WHERE id = @Id AND revoked_at IS NULL";

        private const string RevokeFamilySql = "UPDATE session SET revoked_at = @At WHERE family_id = @FamilyId AND revoked_at IS NULL";

        // "그 기기의 토큰 전부"(2.3절 [임시]): 같은 device_id 의 모든 활성 세션. device_id 는 (계정, 기기) 한 쌍의 번호다
        private const string RevokeDeviceSql = "UPDATE session SET revoked_at = @At WHERE device_id = @DeviceId AND revoked_at IS NULL";

        // 비밀번호 변경·재설정·계정 삭제·제재: 그 계정의 모든 기기
        private const string RevokeAccountSql = "UPDATE session SET revoked_at = @At WHERE account_id = @AccountId AND revoked_at IS NULL";

        // 정리: 갱신 만료나 폐기가 cutoff 보다 오래된 세션
        private const string DeleteSettledSql =
            "DELETE FROM session WHERE expires_at < @Cutoff OR (revoked_at IS NOT NULL AND revoked_at < @Cutoff)";

        // 8.2절: 30일 + 여유 뒤 삭제
        private const string DeleteRefreshUsedSql = "DELETE FROM session_refresh_used WHERE at < @Cutoff";

        public Task<long> InsertAsync(IUnitOfWork uow, NewSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }
            return StoreSql.Scalar<long>(uow, InsertSql, new
            {
                session.AccountId,
                session.DeviceId,
                session.AccessHash,
                session.RefreshHash,
                session.FamilyId,
                RefreshExpiresAt = StoreSql.Utc(session.RefreshExpiresAt),
                AccessExpiresAt = StoreSql.Utc(session.AccessExpiresAt),
            });
        }

        public Task<SessionRow?> FindActiveByAccessHashAsync(IUnitOfWork uow, byte[] accessHash, DateTime now) =>
            StoreSql.One<SessionRow>(uow, FindActiveByAccessSql, new { AccessHash = accessHash, Now = StoreSql.Utc(now) });

        // 폐기·만료 여부와 상관없이 그 해시의 세션을 잠근다. 상태 판정(revoked_at, expires_at)은 호출자가 한다(2.3절 의사코드)
        public Task<SessionRow?> LockByRefreshHashAsync(IUnitOfWork uow, byte[] refreshHash) =>
            StoreSql.One<SessionRow>(uow, LockByRefreshSql, new { RefreshHash = refreshHash });

        public Task<SessionRow?> GetAsync(IUnitOfWork uow, long sessionId) => StoreSql.One<SessionRow>(uow, GetSql, new { Id = sessionId });

        public Task<RefreshUsedRow?> FindRefreshUsedAsync(IUnitOfWork uow, byte[] refreshHash) =>
            StoreSql.One<RefreshUsedRow>(uow, FindRefreshUsedSql, new { RefreshHash = refreshHash });

        // 지금 갱신 토큰을 session_refresh_used 에 남기고 새 두 해시로 바꾼다. 살아 있는 세션이 아니면 false(아무것도 바꾸지 않는다)
        public async Task<bool> RotateAsync(IUnitOfWork uow, long sessionId, byte[] newAccessHash, DateTime accessExpiresAt,
            byte[] newRefreshHash, DateTime refreshExpiresAt, DateTime at)
        {
            if (newAccessHash == null || newAccessHash.Length == 0)
            {
                throw new ArgumentException("access token hash is required", nameof(newAccessHash));
            }
            if (newRefreshHash == null || newRefreshHash.Length == 0)
            {
                throw new ArgumentException("refresh token hash is required", nameof(newRefreshHash));
            }
            SessionRow? s = await StoreSql.One<SessionRow>(uow, GetSql + " FOR UPDATE", new { Id = sessionId }).ConfigureAwait(false);
            if (s == null || s.RevokedAt != null)
            {
                return false;
            }
            await StoreSql.Execute(uow, InsertRefreshUsedSql, new { Id = sessionId, At = StoreSql.Utc(at) }).ConfigureAwait(false);
            return await StoreSql.Exactly1(uow, RotateSql, new
            {
                Id = sessionId,
                AccessHash = newAccessHash,
                AccessExpiresAt = StoreSql.Utc(accessExpiresAt),
                RefreshHash = newRefreshHash,
                RefreshExpiresAt = StoreSql.Utc(refreshExpiresAt),
            }).ConfigureAwait(false);
        }

        public Task<bool> RevokeAsync(IUnitOfWork uow, long sessionId, DateTime at) =>
            StoreSql.Exactly1(uow, RevokeOneSql, new { Id = sessionId, At = StoreSql.Utc(at) });

        public Task<int> RevokeFamilyAsync(IUnitOfWork uow, Guid familyId, DateTime at) =>
            StoreSql.Execute(uow, RevokeFamilySql, new { FamilyId = familyId, At = StoreSql.Utc(at) });

        public Task<int> RevokeDeviceAsync(IUnitOfWork uow, long deviceId, DateTime at) =>
            StoreSql.Execute(uow, RevokeDeviceSql, new { DeviceId = deviceId, At = StoreSql.Utc(at) });

        public Task<int> RevokeAccountAsync(IUnitOfWork uow, long accountId, DateTime at) =>
            StoreSql.Execute(uow, RevokeAccountSql, new { AccountId = accountId, At = StoreSql.Utc(at) });

        public Task<int> DeleteSettledBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteSettledSql, new { Cutoff = StoreSql.Utc(cutoff) });

        public Task<int> DeleteRefreshUsedBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteRefreshUsedSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
