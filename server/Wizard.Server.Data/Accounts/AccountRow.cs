using System;

namespace Wizard.Server.Data.Accounts
{
    // account 표 한 줄(spec 12 8.2절 + CONTRACTS 12.1절 play_seconds). 열을 더하면 PgAccountStore.Columns 를 같이 바꾼다.
    // 이메일은 암호문(EmailEnc)과 찾기용 HMAC(EmailHmac)뿐이다. 주소가 필요하면 EmailProtector.Decrypt(Id, EmailEnc).
    public sealed class AccountRow
    {
        public long Id { get; set; }
        public string LoginId { get; set; } = "";
        public byte[] PwHash { get; set; } = Array.Empty<byte>();
        public string PwParams { get; set; } = "";              // jsonb 원문(2.2절 pw_params)
        public byte[] EmailEnc { get; set; } = Array.Empty<byte>();
        public byte[] EmailHmac { get; set; } = Array.Empty<byte>();
        public DateTime? EmailVerifiedAt { get; set; }
        public string Nickname { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public long Flags { get; set; }                          // 비트: 1=검토 표시, 2=검증 실패 이력, 4=결제 막음 [임시]
        public DateTime? SanctionUntil { get; set; }             // sanction 표의 캐시 [임시 해석]
        public long PlaySeconds { get; set; }                    // CONTRACTS 12.4절. 허브가 센다
        public short League { get; set; }                        // 0 새싹, 1 맹수, 2 후계(8.1절)
        public long? LeagueSeasonId { get; set; }
        public bool ShowNameInAlerts { get; set; }
        public bool PhantomMet { get; set; }
        public DateTime? RevengeDoneAt { get; set; }
        public int PhantomTries { get; set; }
        public int PhantomBestDent { get; set; }
        public int RevengeRuns { get; set; }
        public DateTime? TradeHoldUntil { get; set; }            // 2.6절
        public DateTime? DeletionRequestedAt { get; set; }       // 2.7절
        public int MaxWaveReached { get; set; }
    }
}
