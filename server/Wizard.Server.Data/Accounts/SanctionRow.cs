using System;

namespace Wizard.Server.Data.Accounts
{
    // sanction 표 한 줄(spec 12 8.2절). Until = null 이면 영구. By = 'operator:<name>' | 'system' [임시]
    public sealed class SanctionRow
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public string Kind { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime? Until { get; set; }
        public string By { get; set; } = "";

        public SanctionKind SanctionKind => AccountDbNames.ParseSanction(Kind);
    }
}
