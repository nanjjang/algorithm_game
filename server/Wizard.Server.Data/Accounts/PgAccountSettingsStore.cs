using System;
using System.Threading.Tasks;

namespace Wizard.Server.Data.Accounts
{
    // account_settings 표의 저장소. 계정당 한 줄이고 PUT 이 통째로 바꾼다(GET/PUT /v1/settings, CONTRACTS 8.1절)
    public sealed class PgAccountSettingsStore
    {
        private const string GetSql =
            "SELECT account_id AS AccountId, settings::text AS Settings, updated_at AS UpdatedAt FROM account_settings WHERE account_id = @AccountId";

        private const string PutSql =
            "INSERT INTO account_settings (account_id, settings, updated_at) VALUES (@AccountId, @Settings::jsonb, @At) " +
            "ON CONFLICT (account_id) DO UPDATE SET settings = EXCLUDED.settings, updated_at = EXCLUDED.updated_at";

        public Task<AccountSettingsRow?> GetAsync(IUnitOfWork uow, long accountId) =>
            StoreSql.One<AccountSettingsRow>(uow, GetSql, new { AccountId = accountId });

        public Task<int> PutAsync(IUnitOfWork uow, long accountId, string settingsJson, DateTime at)
        {
            if (settingsJson == null)
            {
                throw new ArgumentNullException(nameof(settingsJson));
            }
            return StoreSql.Execute(uow, PutSql, new { AccountId = accountId, Settings = settingsJson, At = StoreSql.Utc(at) });
        }
    }
}
