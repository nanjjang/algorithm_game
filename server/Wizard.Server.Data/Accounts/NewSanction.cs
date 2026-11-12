using System;

namespace Wizard.Server.Data.Accounts
{
    public sealed class NewSanction
    {
        public NewSanction(long accountId, SanctionKind kind, string reason, DateTime from, DateTime? until, string by)
        {
            if (until.HasValue && until.Value <= from)
            {
                throw new ArgumentException("a sanction must end after it starts", nameof(until));
            }
            AccountId = accountId;
            Kind = kind;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            From = from;
            Until = until;
            By = by ?? throw new ArgumentNullException(nameof(by));
        }

        public long AccountId { get; }
        public SanctionKind Kind { get; }
        public string Reason { get; }
        public DateTime From { get; }
        public DateTime? Until { get; }      // null = 영구
        public string By { get; }
    }
}
