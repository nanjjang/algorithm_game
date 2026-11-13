using System;

namespace Wizard.Client.Duel
{
    // DuelSession 의 값. 명세(spec 10)와 넷 절(data/rules/net.json)에 없는 값이라 부르는 쪽이 준다(코드에 박지 않는다).
    // 데이터로 옮길지는 열린 질문으로 올린다(결과 보고).
    public sealed class DuelSessionOptions
    {
        // Hello 의 client_build(10.4)
        public uint ClientBuild;

        // 연결 절차에서 답(SyncReply, HelloAck)을 기다리다 다시 보내기까지(µs). 1 이상
        public long HandshakeRetryUs;

        public void Validate()
        {
            if (HandshakeRetryUs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(HandshakeRetryUs), "HandshakeRetryUs must be positive");
            }
        }
    }
}
