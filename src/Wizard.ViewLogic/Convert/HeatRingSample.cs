using Wizard.Sim.Events;
using Wizard.Sim.Heat;

namespace Wizard.ViewLogic.Convert
{
    // 열 링(spec 01 17.1 HeatRingState·HeatRatio·LockRemainRatio, effects 10절). PlayerView 의 값만 읽는다.
    //   차가움: 링이 보이지 않는다(눈금만 희미하게). 오르는 중·임박·임계·넘치기 직전: 열 ÷ 100 만큼 찬 링.
    //   잠금: 가득 찬 재색 링이 남은 잠금 ÷ 전체 잠금 만큼 남는다(시계 방향으로 줄어든다).
    //   눈금: 그 캐릭터의 임계 시작값 자리(CritStartHu ÷ 100). 열 면제 대기: 바깥에 가는 흰 링 한 겹.
    public readonly struct HeatRingSample
    {
        public readonly HeatRingState State;
        public readonly float HeatRatio;          // 0~1 = HeatHu ÷ 12,000(100). 한 발이 100을 넘겨도 1에서 멈춘다
        public readonly float CritMarkRatio;      // 0~1 = CritStartHu ÷ 12,000
        public readonly float LockRemainRatio;    // 0~1 = LockRemainTicks ÷ LockTotalTicks. 잠금이 아니면 0
        public readonly bool ExemptArmed;

        public HeatRingSample(HeatRingState state, float heatRatio, float critMarkRatio, float lockRemainRatio, bool exemptArmed)
        {
            State = state;
            HeatRatio = heatRatio;
            CritMarkRatio = critMarkRatio;
            LockRemainRatio = lockRemainRatio;
            ExemptArmed = exemptArmed;
        }

        // 차가움이면 링을 그리지 않는다(effects 10)
        public bool RingVisible => State != HeatRingState.Cold;

        public bool Locked => State == HeatRingState.Locked;

        // 링이 찬 비율: 잠금이면 남은 잠금, 아니면 열
        public float FillRatio => Locked ? LockRemainRatio : HeatRatio;

        // 임계 표식(흰색 + 바깥 검정)을 쓰는 상태
        public bool CriticalMark => State == HeatRingState.Critical || State == HeatRingState.Brink;

        public static HeatRingSample From(in PlayerView p)
        {
            bool locked = p.HeatRing == HeatRingState.Locked;
            float lockRemain = locked && p.LockTotalTicks > 0 ? Clamp01((float)p.LockRemainTicks / p.LockTotalTicks) : 0f;
            return new HeatRingSample(p.HeatRing, Clamp01((float)p.HeatHu / HeatRules.MaxHeatHu),
                                      Clamp01((float)p.CritStartHu / HeatRules.MaxHeatHu), lockRemain, p.ExemptArmed);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
