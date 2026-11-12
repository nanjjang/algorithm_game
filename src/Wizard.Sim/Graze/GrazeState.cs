using Wizard.Core;

namespace Wizard.Sim.Graze
{
    // spec 01 3.9 의 PlayerState 중 Graze 모듈(절 3)의 스칼라 필드. 이미 인정한 시전 묶음(GrazedCasts)과
    // 이번 틱의 후보는 크기가 정해진 목록이라 SimWorld.Graze.cs 가 따로 가진다.
    // 연속 스침 음높이(3.9 의 GrazeChain)는 표시 전용 값이라 여기 두지 않는다(CONTRACTS 0.3 통합 표, 1.4 "표시 전용 값은 넣지 않는다", T-50).
    // 표시 계층이 확정된 GrazeAwarded 의 Tick 으로 센다.
    internal struct GrazeState
    {
        public uint BuffUntil;             // 스침 가속 기한. t < BuffUntil 이면 가속(8.4, 중첩 없이 갱신)
        public bool BuffActive;            // GrazeBuffStarted 를 내고 아직 GrazeBuffEnded 를 내지 않았다
        public uint CooldownReadyTick;     // 스침 간격(결투 1.0초, 12.2 의 DuelGrazeReadyTick). t < 이 값이면 연출만
        public uint AwardTick;             // AwardCount 를 센 틱
        public int AwardCount;             // AwardTick 에 인정된(보상이 있는) 스침 수. 시전마다 1(8.3)

        // 바이트: buff_until u32, buff_active bool, cooldown_ready u32, award_tick u32, award_count i32
        public void Write(ref StateWriter w)
        {
            w.U32(BuffUntil);
            w.Bool(BuffActive);
            w.U32(CooldownReadyTick);
            w.U32(AwardTick);
            w.I32(AwardCount);
        }

        public void Read(ref StateReader r)
        {
            BuffUntil = r.U32();
            BuffActive = r.Bool();
            CooldownReadyTick = r.U32();
            AwardTick = r.U32();
            AwardCount = r.I32();
            if (AwardCount < 0)
            {
                throw new SimInvariantException("graze: negative award count");
            }
        }
    }
}
