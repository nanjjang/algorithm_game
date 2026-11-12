using System;

namespace Wizard.Content.Rules
{
    // CONTRACTS 2.2. 넷코드 값. 시뮬레이션 결과를 바꾸지 않으므로 해시 밖(data/rules/net.json, 섹션 34)이다.
    // 코드에 60/90 같은 값을 박지 않는다. 명세에 없는 처음 값은 [총괄 제안]이고 M2-NET-06 의 측정으로 고친다
    // (어느 값이 제안인지는 data/rules/net.json 의 "_note")
    public sealed class NetRuleSet
    {
        // 왕복 경계 ms → D. 경계 엄격히 오름차순, D 비감소. 왕복이 마지막 줄의 경계를 넘으면 마지막 줄의 D 를 쓴다
        // (spec 10 14.5 "120ms 초과 4틱 고정"). 처음 값: ≤60 → 2, ≤90 → 3, ≤120 → 4 (그 위도 4)
        public InputDelayStep[] InputDelayTable = Array.Empty<InputDelayStep>();
        public int RollbackLimitTicks;                                              // 8
        public int DisconnectSilenceTicks, DisconnectGraceTicks;                    // 끊김 판정까지의 무패킷 틱, 유예(spec 10의 20초 = 1200틱)
        public int FullStateGapTicks, CatchupTicksPerAdvance;
        public int RttHoldCapMs, QueueRttLimitMs;                                   // QueueRttLimitMs = 120
        public int OrderViolationForfeit;                                           // 30
        public int SpectatorDelayTicks, SpectatorKeyframeTicks;                     // 120, 60
        // 시계 맞추기(spec 10 2.8·14.4, T-60). ClockSyncSamples = 경기 전 Sync 왕복 횟수이자 서버가 사람마다 두는 왕복 표본 창의 크기(16).
        // ClockResyncTicks = 경기 중 다시 재는 간격(60). ClockSlewMaxPpm = 틱 길이 보정의 한도(5,000 = ±0.5%)
        public int ClockSyncSamples, ClockResyncTicks, ClockSlewMaxPpm;
    }
}
