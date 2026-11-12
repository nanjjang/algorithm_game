using System;
using Wizard.Sim.Input;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.2, spec 10 14.3. ClientInput 이 싣는 이벤트 창: 유효 틱이 sealed_through_tick − 7 이상인 내 이벤트 전부.
    // 아직 봉인하지 않은 다음 틱(입력 시계 n 에서 유효 틱 n + D = sealed_through_tick + 1)의 이벤트도 싣는다.
    // 최근 8틱을 겹쳐 보내므로 패킷 하나가 사라져도 다음 패킷이 메운다.
    public static class ClientInputWindow
    {
        // 창의 첫 틱. 경기 처음(sealed < 7)에는 0
        public static uint FirstTick(uint sealedThroughTick)
        {
            const uint back = DuelWire.OverlapTicks - 1;
            return sealedThroughTick >= back ? sealedThroughTick - back : 0U;
        }

        public static bool Contains(uint sealedThroughTick, uint tick) => tick >= FirstTick(sealedThroughTick);

        // packet.SealedThroughTick 을 먼저 정해 두고 부른다. history 는 내가 낸 이벤트의 순번 순 열이다.
        // 창 안의 이벤트를 순번 순으로 싣고, 64개가 넘으면 가장 오래된 것부터 뺀다(겹쳐 보낸 앞 패킷이 이미 실었다.
        // 정말 닿지 않았으면 서버가 base_seq 로 유실 처리한다, 10.6 (1)).
        // base_seq = 실은 첫 이벤트 바로 앞 순번. 실을 이벤트가 없으면 lastAssignedSeq(지금까지 매긴 마지막 순번).
        public static void Fill(ClientInputPacket packet, ReadOnlySpan<InputEvent> history, uint lastAssignedSeq)
        {
            packet.ClearEvents();
            uint first = FirstTick(packet.SealedThroughTick);
            int inWindow = 0;
            for (int i = 0; i < history.Length; i++)
            {
                if (history[i].Tick >= first)
                {
                    inWindow++;
                }
            }
            int skip = inWindow > DuelWire.MaxClientEvents ? inWindow - DuelWire.MaxClientEvents : 0;
            for (int i = 0; i < history.Length; i++)
            {
                if (history[i].Tick < first)
                {
                    continue;
                }
                if (skip > 0)
                {
                    skip--;
                    continue;
                }
                packet.TryAddEvent(history[i]);
            }
            packet.BaseSeq = packet.EventCount > 0 ? packet.Events[0].Seq - 1U : lastAssignedSeq;
        }
    }
}
