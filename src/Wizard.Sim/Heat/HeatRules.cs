using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.Sim.Heat
{
    // spec 01 2.4(열의 단위), 7.3(임계 시작), 7.7(파생 값). 순수 함수이고 상태가 없다.
    // 세계(Heat 모듈), 야영 화면의 수치 칸(GDD 13), 컴파일러가 같은 식을 쓴다.
    public static class HeatRules
    {
        // spec 01 2.4: 열 1 = 120 HU(단위 환산). 원문의 모든 열 연산이 이 단위에서 정수로 떨어진다
        public const int HuPerHeat = 120;

        // spec 01 2.4 표 "열 최대(잠금 경계) 100 → 12,000". 게이지의 눈금 자체(열 ÷ 100 = HeatRatio, 17.1)라 캐릭터마다 같다(7.1)
        public const int MaxHeatHu = 12000;

        // spec 01 7.7 열 링 표의 경계 [제안](eff 10): 임박 = 임계 시작 − 15부터, 넘치기 직전 = 95부터(2.4 표 11,400).
        // 표시용 경계라 판정에는 쓰지 않는다. 데이터 절이 생기면 그리로 옮긴다(결과 보고의 열린 질문)
        public const int ImminentSpanHu = 1800;
        public const int BrinkHu = 11400;

        // CritShotCount 의 "∞"(발사당 열 0). 표시 "∞"
        public const int CritShotCountUnlimited = int.MaxValue;

        // spec 01 7.3 CritStartHu: 임계 시작값을 바꾸는 효과가 여럿이면 가장 낮은 값(GDD 9).
        // characterHu = 캐릭터 값(달구는 지팡이 특성을 반영한 80, spec 01 15절 C17). capHu = 효과의 임계 시작(과민 회로 70 → 8,400), 0이면 없음
        public static int CritStartHu(int characterHu, int capHu)
        {
            if (capHu > 0 && capHu < characterHu)
            {
                return capHu;
            }
            return characterHu;
        }

        // spec 01 7.3: 정확히 100(12,000 HU)은 잠금이 아니다(">100")
        public static bool LocksAfter(int heatAfterHu) => heatAfterHu > MaxHeatHu;

        // spec 01 7.7 임계 사격 가능 횟수 n: 열이 임계 시작값 바로 아래에서 시작해 잠그지 않고 이어 쏠 수 있는 임계 발수.
        // 0이면 야영 화면이 경고를 띄운다(GDD 13)
        public static int CritShotCount(int shotHeatHu, int critStartHu)
        {
            if (shotHeatHu <= 0)
            {
                return CritShotCountUnlimited;
            }
            if (shotHeatHu > MaxHeatHu)
            {
                return 0;
            }
            int span = MaxHeatHu - critStartHu;
            if (span < 0)
            {
                return 0;
            }
            return FixMath.DivInt(span, shotHeatHu) + 1;
        }

        // spec 01 7.7 열 링 상태(표시 계층용). 잠금이 가장 앞이다
        public static HeatRingState Ring(int heatHu, int critStartHu, bool locked)
        {
            if (locked)
            {
                return HeatRingState.Locked;
            }
            if (heatHu <= 0)
            {
                return HeatRingState.Cold;
            }
            if (heatHu < critStartHu - ImminentSpanHu)
            {
                return HeatRingState.Rising;
            }
            if (heatHu < critStartHu)
            {
                return HeatRingState.Imminent;
            }
            if (heatHu < BrinkHu)
            {
                return HeatRingState.Critical;
            }
            return HeatRingState.Brink;
        }

        // 냉각 한 틱의 양(spec 01 7.4): 캐릭터 냉각(틱당 HU) × 냉각 배율(천분율, 탐식 500). 기준 90 → 45, 초롱 100 → 50
        public static int CoolPerTickHu(int coolHuPerTick, int coolPermille) =>
            (int)((long)coolHuPerTick * coolPermille / PermilleOne);

        // 방열 폭발의 피해 계산용 열(spec 01 7.6.7): 잠금 중이면 100, 아니면 열의 정수 부분(내림) [임시]
        public static int VentBlastHeat(int heatHu, bool locked) => locked ? MaxHeatHu / HuPerHeat : (heatHu > 0 ? heatHu / HuPerHeat : 0);

        private const int PermilleOne = 1000;    // 천분율 1.0(단위 환산)
    }
}
