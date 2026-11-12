using System;

namespace Wizard.Server.Data.Accounts
{
    // account_settings 표 한 줄(CONTRACTS 12.2절, spec 13 13절 설정 동기화). Settings 는 jsonb 원문(SettingsData), UpdatedAt 은 서버 시각
    public sealed class AccountSettingsRow
    {
        public long AccountId { get; set; }
        public string Settings { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
