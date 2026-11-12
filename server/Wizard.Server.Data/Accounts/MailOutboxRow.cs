using System;

namespace Wizard.Server.Data.Accounts
{
    // mail_outbox 표 한 줄(spec 12 2.8절). 받는 주소와 본문은 없다(보낼 때 account 에서 읽는다)
    public sealed class MailOutboxRow
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "";
        public long? AccountId { get; set; }
        public string Status { get; set; } = "";
        public short Tries { get; set; }
        public string? ProviderRef { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? NextTryAt { get; set; }
        public long? TokenId { get; set; }

        public MailOutboxKind MailKind => AccountDbNames.ParseMailKind(Kind);

        public MailOutboxStatus MailStatus => AccountDbNames.ParseMailStatus(Status);

        // 사업자가 받았다가 반송·거절한 메일(DECISIONS-TECH T-56). 보내지 못하고 버린 줄에는 sent_at 이 없다
        public bool Bounced => MailStatus == MailOutboxStatus.Dropped && SentAt.HasValue;
    }
}
