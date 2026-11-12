using System;

namespace Wizard.Server.Data.Accounts
{
    // account_token 표 한 줄(spec 12 8.2절). 코드는 해시(TokenHash)만 있다. 바꿀 주소는 암호문(NewEmailEnc)만 있다
    public sealed class AccountTokenRow
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public string Kind { get; set; } = "";                    // 'verify_email' | 'reset_password' | 'change_email'
        public byte[] TokenHash { get; set; } = Array.Empty<byte>();
        public byte[]? NewEmailEnc { get; set; }                  // change_email 전용
        public short Attempts { get; set; }                       // 틀린 횟수(0~5)
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }                     // 쓴 때 또는 폐기한 때
        public DateTime CreatedAt { get; set; }

        public AccountTokenKind TokenKind => AccountDbNames.ParseTokenKind(Kind);
    }
}
