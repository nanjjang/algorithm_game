using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // device 표의 저장소(spec 12 2.5~2.6절). device_hash = HMAC-SHA256(device_key, 기기 값)은 App 이 만든다(원문 값은 저장하지 않는다).
    public sealed class PgDeviceStore
    {
        // 처음이면 넣는다. 같은 (계정, 기기)의 동시 로그인은 유일 제약에서 앞의 트랜잭션을 기다린 뒤 0행이 되고 아래 UPDATE 로 간다
        private const string InsertSql =
            "INSERT INTO device (account_id, device_hash, first_seen, last_seen) VALUES (@AccountId, @DeviceHash, @At, @At) " +
            "ON CONFLICT (account_id, device_hash) DO NOTHING RETURNING id";

        private const string TouchSql =
            "UPDATE device SET last_seen = GREATEST(last_seen, @At) WHERE account_id = @AccountId AND device_hash = @DeviceHash RETURNING id";

        // 기기당 가입 수와 자동 매치 3계정(2.5절)의 바탕
        private const string CountAccountsSql = "SELECT count(DISTINCT account_id) FROM device WHERE device_hash = @DeviceHash";

        private const string ListAccountsSql = "SELECT DISTINCT account_id FROM device WHERE device_hash = @DeviceHash ORDER BY account_id";

        public async Task<DeviceSeen> TouchAsync(IUnitOfWork uow, long accountId, byte[] deviceHash, DateTime at)
        {
            if (deviceHash == null || deviceHash.Length == 0)
            {
                throw new ArgumentException("device hash is required", nameof(deviceHash));
            }
            var p = new { AccountId = accountId, DeviceHash = deviceHash, At = StoreSql.Utc(at) };
            IReadOnlyList<long> inserted = await StoreSql.Many<long>(uow, InsertSql, p).ConfigureAwait(false);
            if (inserted.Count == 1)
            {
                return new DeviceSeen(inserted[0], true);
            }
            IReadOnlyList<long> touched = await StoreSql.Many<long>(uow, TouchSql, p).ConfigureAwait(false);
            if (touched.Count != 1)
            {
                throw new InvalidOperationException("device row vanished between insert and update");
            }
            return new DeviceSeen(touched[0], false);
        }

        public Task<int> CountAccountsAsync(IUnitOfWork uow, byte[] deviceHash) =>
            StoreSql.Count(uow, CountAccountsSql, new { DeviceHash = deviceHash });

        public Task<IReadOnlyList<long>> ListAccountsAsync(IUnitOfWork uow, byte[] deviceHash) =>
            StoreSql.Many<long>(uow, ListAccountsSql, new { DeviceHash = deviceHash });
    }
}
