using Wizard.Sim;

namespace Wizard.BalanceLab.Feel
{
    // GDD 16절 1단계 통과 기준 네 가지의 목표값과, 판정에 쓰는 판정기 상수. 측정값은 이 값을 바꾸지 않는다(CLAUDE.md 5절).
    // 도구가 명세의 목표를 비교하려고 적어 둔 값이다. 게임 안의 수치가 아니므로 data/ 에 두지 않았다(결과 보고의 열린 질문).
    internal static class FeelTargets
    {
        // GDD 16절 1단계 "통과 기준"과 spec 03 22절의 같은 문장
        public const int FireRatioMinPermille = 1400;          // 임계 구간 플레이가 꾹 누르기보다 1.4배 이상 강함
        public const int TwoPressMinPermille = 900;            // 두 번 누르는 커맨드 성공률 90% 이상
        public const int JustMinPermille = 300;                // 10분 연습 후 섬 성공률 30% 이상
        public const int PracticeMinutes = 10;                 // 위 "10분 연습"
        public const int MisfireBelowPermille = 100;           // 자세 중 이동하다 엉뚱한 기술이 나가는 비율 10% 미만

        // GDD 18절 "검증 요약": 마력탄(피해 10, 열 4, 간격 0.15초, 임계 2배), 기준값(임계 85, 냉각 45, 잠금 3초), 강화 +0.
        // 연속 시간 식으로 계산한 값이라 틱 단위로 도는 세계의 값과 조금 다를 수 있다. 판정이 아니라 나란히 보여 주는 참고값이다
        public const long GddHoldDpsMilli = 44900;
        public const long GddCritDpsMilli = 69200;
        public const long GddRatioPermille = 1540;

        // 임계 배율 ×2(spec 02 11절, GDD 5절). 팩에 없는 값이라 여기 둔다(코어의 손맛 시제품 수치에도 같은 값이 있다)
        public const int CritMulPermille = 2000;

        // 3갈래 분열의 열 배수 ×2.2(spec 01 2.4 표, spec 02 4.2). 팩에 분열 블록이 생기면(M2-SPL-01) 거기서 읽는다
        public const int SplitThreeHeatMulPermille = 2200;

        // 봇의 자리 잡기(FireRatioMeter): 아래 벽까지 걷고(WalkTicks 동안 아래 키) 선다(SettleTicks 까지). 포탑 사거리 밖이어야 하고,
        // 설 때쯤 이미 오른 열(스침 보상 85)이 식을 시간도 준다. 단위는 틱 [임시: 명세에 없음]
        public const uint WalkTicks = 90;
        public const uint SettleTicks = 180;

        // 판정기 상수(spec 03 1.2): 섬 0.07초, 마지막 방향에서 K 까지의 한도 0.3초. 입력 시각 단위(1/15,360초)
        public static readonly long JustWindowUnits = SimTime.InputUnitsFromDecimalSeconds(7, 2);
        public static readonly long GapDefaultUnits = SimTime.InputUnitsFromDecimalSeconds(3, 1);

        // "두 번 누르는 커맨드를 시도했다"고 보는 창: 첫째 앞이 K 보다 이 안에 있어야 한다 [임시: 명세에 없음. 결과 보고의 열린 질문].
        // 판정기의 한도(0.3초)보다 넉넉히 잡아 "너무 느려서 끊긴" 시도도 실패로 센다
        public const int AttemptWindowMsDefault = 1000;

        // 간격 분포의 구간 끝(밀리초). 표시용이고 판정에 쓰지 않는다. 70은 섬, 300은 판정기 한도다
        public static readonly int[] GapEdgesMs = { 70, 100, 150, 200, 300 };
    }
}
