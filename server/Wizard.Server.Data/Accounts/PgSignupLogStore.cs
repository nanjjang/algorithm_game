using System;
using System.Net;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // signup_log 표의 저장소(spec 12 2.5절 IP·기기당 하루 3계정). "하루" = 지난 24시간 이동 창 [임시]: since = now − 24h
    public sealed class PgSignupLogStore
    {
        private const string InsertSql = "INSERT INTO signup_log (ip, device_hash, at) VALUES (@Ip::inet, @DeviceHash, @At)";

        private const string CountByIpSql = "SELECT count(*) FROM signup_log WHERE ip = @Ip::inet AND at > @Since";

        private const string CountByDeviceSql = "SELECT count(*) FROM signup_log WHERE device_hash = @DeviceHash AND at > @Since";

        private const string DeleteBeforeSql = "DELETE FROM signup_log WHERE at < @Cutoff";

        public Task<int> InsertAsync(IUnitOfWork uow, IPAddress ip, byte[] deviceHash, DateTime at)
        {
            if (deviceHash == null || deviceHash.Length == 0)
            {
                throw new ArgumentException("device hash is required", nameof(deviceHash));
            }
            return StoreSql.Execute(uow, InsertSql, new { Ip = StoreSql.IpText(ip), DeviceHash = deviceHash, At = StoreSql.Utc(at) });
        }

        public Task<int> CountByIpSinceAsync(IUnitOfWork uow, IPAddress ip, DateTime since) =>
            StoreSql.Count(uow, CountByIpSql, new { Ip = StoreSql.IpText(ip), Since = StoreSql.Utc(since) });

        public Task<int> CountByDeviceSinceAsync(IUnitOfWork uow, byte[] deviceHash, DateTime since) =>
            StoreSql.Count(uow, CountByDeviceSql, new { DeviceHash = deviceHash, Since = StoreSql.Utc(since) });

        public Task<int> DeleteBeforeAsync(IUnitOfWork uow, DateTime cutoff) =>
            StoreSql.Execute(uow, DeleteBeforeSql, new { Cutoff = StoreSql.Utc(cutoff) });
    }
}
