using System;

namespace Wizard.Server.Data.Accounts
{
    // 로그인 한 번으로 생기는 세션(토큰 사슬의 첫 고리). FamilyId 는 호출자가 새로 만든다(2.3절 family_id)
    public sealed class NewSession
    {
        public NewSession(long accountId, long deviceId, byte[] accessHash, DateTime accessExpiresAt, byte[] refreshHash, DateTime refreshExpiresAt, Guid familyId)
        {
            if (accessHash == null || accessHash.Length == 0)
            {
                throw new ArgumentException("access token hash is required", nameof(accessHash));
            }
            if (refreshHash == null || refreshHash.Length == 0)
            {
                throw new ArgumentException("refresh token hash is required", nameof(refreshHash));
            }
            AccountId = accountId;
            DeviceId = deviceId;
            AccessHash = accessHash;
            AccessExpiresAt = accessExpiresAt;
            RefreshHash = refreshHash;
            RefreshExpiresAt = refreshExpiresAt;
            FamilyId = familyId;
        }

        public long AccountId { get; }
        public long DeviceId { get; }
        public byte[] AccessHash { get; }
        public DateTime AccessExpiresAt { get; }
        public byte[] RefreshHash { get; }
        public DateTime RefreshExpiresAt { get; }
        public Guid FamilyId { get; }
    }
}
