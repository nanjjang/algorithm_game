using System;
using Wizard.Core;

namespace Wizard.Client.Duel
{
    // LoopbackHost 가 DuelHostCore 와 주고받는 것을 그대로 보는 자리(기록, 계측, 시험). 바꾸지 못한다.
    // 같은 호출 열을 다른 DuelHostCore(예: wizard-duel)에 주면 같은 바이트가 나와야 한다(호스트는 시계를 모른다).
    public interface ILoopbackTap
    {
        // host.OnPacket(packet, receiveMatchUs, fromAddress) 직전
        void OnHostPacket(ReadOnlySpan<byte> packet, long receiveMatchUs, ulong fromAddress);

        // host.Advance(matchUs) 직전
        void OnHostAdvance(long matchUs);

        // host.PollOutgoing(to, …) 이 낸 패킷
        void OnHostSend(PlayerSlot to, ReadOnlySpan<byte> packet, ulong toAddress);
    }
}
