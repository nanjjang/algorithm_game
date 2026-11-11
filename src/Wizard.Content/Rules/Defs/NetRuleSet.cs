using System;

namespace Wizard.Content.Rules
{
    // CONTRACTS 2.2. 넷코드 값. 시뮬레이션 결과를 바꾸지 않으므로 해시 밖(data/rules/net.json, 섹션 34)이다.
    // 코드에 60/90 같은 값을 박지 않는다. 명세에 없는 처음 값은 [총괄 제안]이고 M2-NET-06 의 측정으로 고친다
    public sealed class NetRuleSet
    {
        public InputDelayStep[] InputDelayTable = Array.Empty<InputDelayStep>();   // 왕복 경계 ms → D
        public int RollbackLimitTicks;                                              // 8
        public int DisconnectSilenceTicks, DisconnectGraceTicks;                    // 끊김 판정까지의 무패킷 틱, 유예(spec 10의 20초)
        public int FullStateGapTicks, CatchupTicksPerAdvance;
        public int RttHoldCapMs, QueueRttLimitMs;                                   // QueueRttLimitMs = 120
        public int OrderViolationForfeit;                                           // 30
        public int SpectatorDelayTicks, SpectatorKeyframeTicks;                     // 120, 60
    }
}
