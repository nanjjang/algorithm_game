using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, DECISIONS-TECH T-53. 블록마다 다른 동작 값(spec 02 4.1 메모, 4.2 "기타", 9.3, 10.4, 12절, 18절 U6~U12).
    // 그 블록이 쓰지 않는 값은 0 이다(원본에서 빼면 0). 어느 블록이 어느 값을 가져야 하는지는 변환기가 본다
    // (tools/Wizard.DataBuild/Convert/BlocksConverter.cs). 결투에서 바뀌는 값(감속 절반, 잠식 1.08, 유도 선회 절반)은 결투 규칙 데이터의 몫이다.
    public sealed class BlockParams
    {
        // ── 투사체 ──
        public Fix64 ExplosionRadius;                       // 칸. Explodes: 2 (U11. 적 중심까지의 거리 ≤ 반경 + 적 반지름)
        public int ChainHops;                               // Chains: 전이 3회(첫 타 + 전이 3 = 최대 4타, U10)
        public Fix64 ChainRadius;                           // 칸. Chains: 전이 반경 3
        public int ChainHopMulPermille;                     // Chains: 전이마다 ×0.7 = 700. 피해 계산은 FixMath.PowTable(PowBase.Chain07, k)를 쓴다(변환기가 표와 대조한다)
        public Fix64 ReturnArriveRadius;                    // 칸. Returns: 쏜 사람 반경 0.5 안이면 사라진다 [임시] (U12)
        public Fix64 ReturnMaxDistance;                     // 칸. Returns: 돌아오는 길 9 를 넘으면 사라진다 [임시] (U12)

        // ── 적중 때 거는 상태(spec 02 12절). 얼음 송곳(투사체), 서리·잠식(변형) ──
        public int SlowPermille;                            // 이동 감속. 얼음 송곳 300, 서리 200
        public int SlowTicks;                               // 60
        public int DamageTakenBonusPermille;                // 맞은 적이 받는 피해 증가. 잠식 150
        public int DamageTakenBonusTicks;                   // 180

        // ── 제어(분열) ──
        public int FanHalfAngleCenti;                       // 부채꼴의 반각(센티도). 2갈래 1000(±10도), 3갈래 1500(−15 / 0 / +15도). 갈래는 −반각 ~ +반각을 고르게 나눈다(spec 02 9.3)

        // ── 변형 ──
        public int BounceCount;                             // Bounce: 블록 1개당 튕김 2회 [임시] (U6)
        public int PierceCount;                             // Pierce: 블록 1개당 +2체 (U7)
        public int RadiusMulPermille;                       // Size: 판정 반지름 ×2 = 2000 [임시] (U8)
        public int RadiusMulCapPermille;                    // Size: 겹쳤을 때의 상한 ×4 = 4000 [임시] (U8)
        public int HomingTurnCentiPerTick;                  // Homing: 틱당 선회각 300 센티도(초당 180도) [임시] (U9)
        public int HomingConeHalfCenti;                     // Homing: 대상을 찾는 시야 반각 9000 센티도(앞 ±90도) [임시] (U9)
        public Fix64 HomingRange;                           // 칸. Homing: 대상을 찾는 거리 6 [임시] (U9)
        public int CritMultiplier;                          // TripleHeat: 임계 배율을 이 값(3)으로 교체. 0 이면 바꾸지 않는다
    }
}
