using System;
using System.Net;

namespace Wizard.Server.Data.Accounts
{
    // 새 코드 한 건. 코드 원문은 넘기지 않는다: 호출자가 만든 해시만(2.9절 HMAC, 2.11절 SHA-256).
    // NewEmail 은 change_email 일 때만 있고 PgAccountTokenStore 가 계정 번호로 암호화해 new_email_enc 에만 넣는다
    public sealed class NewAccountToken
    {
        public NewAccountToken(long accountId, AccountTokenKind kind, byte[] tokenHash, DateTime expiresAt, IPAddress? createdIp, string? newEmail)
        {
            if (tokenHash == null || tokenHash.Length == 0)
            {
                throw new ArgumentException("token hash is required", nameof(tokenHash));
            }
            if ((kind == AccountTokenKind.ChangeEmail) != (newEmail != null))
            {
                throw new ArgumentException("a new email goes with change_email tokens only", nameof(newEmail));
            }
            AccountId = accountId;
            Kind = kind;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
            CreatedIp = createdIp;
            NewEmail = newEmail;
        }

        public long AccountId { get; }
        public AccountTokenKind Kind { get; }
        public byte[] TokenHash { get; }
        public DateTime ExpiresAt { get; }
        public IPAddress? CreatedIp { get; }
        public string? NewEmail { get; }
    }
}
