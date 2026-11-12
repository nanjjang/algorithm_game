using System;
using System.Net;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // login_attempt 표의 저장소(spec 12 2.4절). 대기는 아이디 단위로 센다. 없는 아이디도 같은 방식으로 센다(같은 문구 [임시]).
    // 한도 값(5회, 분당 20회, 900초)은 App 의 것이다.
    public sealed class PgLoginAttemptStore
    {
        private const string InsertSql = "INSERT INTO login_attempt (login_id, ip, at, ok) VALUES (@LoginId, @Ip::inet, @At, @Ok)";

        private const string CountByIpSinceSql = "SELECT count(*) FROM login_attempt WHERE ip = @Ip::inet AND at > @Since";

        // 마지막 성공 뒤의 실패. 성공이 없으면 전부
        private const string FailureStateSql =
            "SELECT count(*) AS Failures, max(at) AS LastFailureAt FROM login_attempt " +
            "WHERE login_id = @LoginId AND NOT ok AND at > COALESCE((SELECT max(at) FROM login_attempt WHERE login_id = @LoginId AND ok), '-infinity')";

        // 8.4절: 30일 뒤 삭제
        private const string DeleteBeforeSql = "DELETE FROM login_attempt WHERE at < @Cutoff";

        public Task<int> InsertAsync(IUnitOfWork uow, string loginId, IPAddress ip, DateTime at, bool ok)
        {
            if (loginId == null)
            {
                throw new ArgumentNullException(nameof(loginId));
            }
            return StoreSql.Execute(uow, InsertSql, new { LoginId = loginId, Ip = StoreSql.IpText(ip), At = StoreSql.Utc(at), Ok = ok });
        }

        public Task<int> CountByIpSinceAsync(IUnitOfWork uow, IPAddress ip, DateTime since) =>
            StoreSql.Count(uow, CountByIpSinceSql, new { Ip = StoreSql.IpText(ip), Since = StoreSql.Utc(since) });

        public async Task<LoginFailureState> GetFailureStateAsync(IUnitOfWork uow, string loginId)
        {
            LoginFailureRow? row = await StoreSql.One<LoginFailureRow>(uow, FailureStateSql, new { LoginId = loginId }).ConfigureAwait(false);
            return row == null ? new LoginFailureState(0, null) : new LoginFailureState(checked((int)row.Failures), row.LastFailureAt);
        }

        public Task<int> DeleteBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteBeforeSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
