using System;

namespace Wizard.Server.Data.Accounts
{
    // session 표 한 줄(spec 12 2.3절, 8.2절). 토큰은 SHA-256 해시만 있다.
    // ExpiresAt = 갱신 토큰 만료 [임시], AccessExpiresAt = 접근 토큰 만료 [임시](15절 C-06)
    public sealed class SessionRow
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public long DeviceId { get; set; }
        public byte[] AccessHash { get; set; } = Array.Empty<byte>();
        public byte[] RefreshHash { get; set; } = Array.Empty<byte>();
        public Guid FamilyId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime AccessExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
