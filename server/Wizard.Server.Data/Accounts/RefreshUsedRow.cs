using System;

namespace Wizard.Server.Data.Accounts
{
    // session_refresh_used 표 한 줄: 회전으로 물러난 갱신 토큰의 해시(재사용 탐지, 2.3절)
    public sealed class RefreshUsedRow
    {
        public byte[] RefreshHash { get; set; } = Array.Empty<byte>();
        public Guid FamilyId { get; set; }
        public long AccountId { get; set; }
        public long DeviceId { get; set; }
        public DateTime At { get; set; }
    }
}
