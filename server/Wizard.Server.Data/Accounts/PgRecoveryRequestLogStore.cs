using System;
using System.Net;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // recovery_request_log 표의 저장소(spec 12 2.11~2.12절). 재설정 요청과 아이디 찾기는 한도를 같이 센다(두 종류를 합쳐 센다).
    // accountId 가 null 인 줄 = 아이디가 없거나 주소에 묶인 계정이 없는 요청(IP 한도에만 든다)
    public sealed class PgRecoveryRequestLogStore
    {
        private const string InsertSql = "INSERT INTO recovery_request_log (account_id, ip, kind, at) VALUES (@AccountId, @Ip::inet, @Kind, @At)";

        private const string CountByAccountSql = "SELECT count(*) FROM recovery_request_log WHERE account_id = @AccountId AND at > @Since";

        private const string CountByIpSql = "SELECT count(*) FROM recovery_request_log WHERE ip = @Ip::inet AND at > @Since";

        private const string DeleteBeforeSql = "DELETE FROM recovery_request_log WHERE at < @Cutoff";

        public Task<int> InsertAsync(IUnitOfWork uow, long? accountId, IPAddress ip, RecoveryRequestKind kind, DateTime at) =>
            StoreSql.Execute(uow, InsertSql, new
            {
                AccountId = accountId,
                Ip = StoreSql.IpText(ip),
                Kind = AccountDbNames.RecoveryKind(kind),
                At = StoreSql.Utc(at),
            });

        public Task<int> CountByAccountSinceAsync(IUnitOfWork uow, long accountId, DateTime since) =>
            StoreSql.Count(uow, CountByAccountSql, new { AccountId = accountId, Since = StoreSql.Utc(since) });

        public Task<int> CountByIpSinceAsync(IUnitOfWork uow, IPAddress ip, DateTime since) =>
            StoreSql.Count(uow, CountByIpSql, new { Ip = StoreSql.IpText(ip), Since = StoreSql.Utc(since) });

        public Task<int> DeleteBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteBeforeSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
