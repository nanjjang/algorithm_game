using System;

namespace Wizard.Client.Duel
{
    // 기기 안 호스트의 값. 명세(spec 10)와 넷 절(data/rules/net.json)에 없는 값이라 부르는 쪽이 준다(코드에 박지 않는다).
    // 데이터로 옮길지는 열린 질문으로 올린다(결과 보고).
    public sealed class LoopbackHostOptions
    {
        // 두 사람의 Hello 가 다 온 때부터 경기 시계의 틱 0(match_epoch)까지의 여유(µs). HelloAck 가 닿고 로딩할 시간이다. 0 이상
        public long StartLeadUs;

        public void Validate()
        {
            if (StartLeadUs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(StartLeadUs), "StartLeadUs must not be negative");
            }
        }
    }
}
