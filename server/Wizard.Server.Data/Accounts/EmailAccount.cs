using System;

namespace Wizard.Server.Data.Accounts
{
    // 같은 찾기용 HMAC 에 묶인 계정 한 줄(아이디 찾기 2.12절, 주소 하나에 3계정 2.5절). 암호문은 비교용으로 같이 싣는다(2.11절: 풀어서 다시 비교)
    public sealed class EmailAccount
    {
        public long Id { get; set; }
        public string LoginId { get; set; } = "";
        public byte[] EmailEnc { get; set; } = Array.Empty<byte>();
        public DateTime? EmailVerifiedAt { get; set; }
    }
}
