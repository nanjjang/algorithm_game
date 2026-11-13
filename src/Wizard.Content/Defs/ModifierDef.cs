using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 07 2.5(변덕 8행), 9절(선택과 적용), spec 14 3.9(코드, 점수 배율), monsters 4.5. 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/modifiers.json. ContentDb.Modifiers 는 ModifierId 오름차순(= spec 07 9절 "데이터 순서 고정"의 후보 순서)이다.
    //
    // 효과 칸은 모든 변덕에 같이 있고, 그 변덕이 쓰지 않는 칸은 "효과 없음" 값(아래 각 칸의 주석)이다. 변덕 2개면 효과가 둘 다 든다.
    // 적용 순서(spec 07 9절 [임시] U-09): 큰 무리(예산) → 우두머리 행차(엘리트) → 긴 밤(가중치·상한) → 구성 생성 → 두꺼운 가죽(체력)
    //   → 몰려옴(간격) → 느린 탄막(적 탄) → 마지막 숨(사망 시) → 좁은 들(WavePrelude).
    // 예고 시간을 줄이거나 적을 빠르게 하는 칸은 두지 않는다(spec 07 2.5, TV-109). 그런 필드가 원본에 있으면 DataBuild 가 거부하고,
    // 적 탄을 빠르게 하는 값(EnemyShotSpeedPct > 100)이나 예고 0 인 구역은 절 읽기도 ContentException 으로 거부한다.
    public sealed class ModifierDef
    {
        public const int NeutralPct = 100;                  // 배율 칸(…Pct)의 "효과 없음"

        public ModifierId Id;

        // 점수 배율(100배 정수, ×1.15 → 115). 변덕이 2개면 차례로 곱하고 매번 100으로 나눠 내린다(spec 07 2.5, 12.2)
        public int ScorePct;

        // 이 변덕이 후보에 드는 시간대. None 이면 모든 시간대(보스 웨이브에는 변덕이 없다, spec 07 9절). 긴 밤 = Night
        public TimeOfDay OnlyTod;

        // ── 몰려옴: 묶음 간격(틱). 0 = 바꾸지 않음(WaveCurveDef.BundleIntervalTicks 420). 몰려옴 240 ──
        public int BundleIntervalTicks;

        // ── 두꺼운 가죽: 적 체력 배율(백분율 정수). 100 = 효과 없음. 두꺼운 가죽 130 ──
        public int EnemyHpPct = NeutralPct;

        // ── 큰 무리: 예산 +ExtraBudgetPct %(내림), 추가분은 비용 ExtraBudgetCostMax 이하의 종으로만. 0, 0 = 효과 없음. 큰 무리 25, 2 ──
        public int ExtraBudgetPct;
        public int ExtraBudgetCostMax;

        // ── 우두머리 행차: 엘리트 수 더하기. 0 = 효과 없음. 우두머리 행차 2 ──
        public int ExtraElites;

        // ── 마지막 숨: 적이 죽은 자리의 원(spec 07 9절: 갈라진 개체·불러낸 개체 포함, 클리어 때 남은 원은 사라진다) ──
        // DeathCircleRadius == 0 이면 효과 없음(나머지 셋도 0). 마지막 숨: 1.2칸(Raw 5,153,960,755), 예고 48틱, 피해 1칸,
        // R5 검사에 걸리면 늦게 생긴 원부터 DeathCircleStaggerTicks(12틱 = 0.2초)씩 미뤄 깐다(TV-105, TV-106)
        public Fix64 DeathCircleRadius;
        public int DeathCircleTelegraphTicks;
        public int DeathCircleDamage;                       // 체력 칸
        public int DeathCircleStaggerTicks;

        // ── 느린 탄막: 적 탄 수 배율(백분율, 탄 수 = floor(n × Pct ÷ 100)), 탄 속도 배율(백분율). 100, 100 = 효과 없음. 느린 탄막 150, 80 ──
        // 탄 속도는 100 을 넘지 못한다(적을 빠르게 하지 않는다, spec 07 2.5). 탄 사이 1.0칸(R3)은 적 명세가 퍼짐 각으로 지킨다
        public int EnemyShotCountPct = NeutralPct;
        public int EnemyShotSpeedPct = NeutralPct;

        // ── 긴 밤: 스폰 가중치에 곱하는 배율(백분율 정수, 시간대 배율 뒤에 곱한다: 밤 200 × 200% → 400). 여기 없는 종은 100 ──
        // WeightSpecies 는 EnemyTypeId 오름차순이고 겹치지 않는다. WeightPct[i] 가 WeightSpecies[i] 의 배율이다. 긴 밤: bat_dusk 200, wisp_stray 200
        public EnemyTypeId[] WeightSpecies = Array.Empty<EnemyTypeId>();
        public int[] WeightPct = Array.Empty<int>();

        // ── 긴 밤: 종 상한을 이 값으로 바꾼다(WaveCurveDef.SpeciesCap 대신). 여기 없는 종은 그대로. 긴 밤: wisp_stray 6 ──
        // CapSpecies 는 EnemyTypeId 오름차순이고 겹치지 않는다. Cap[i] 가 CapSpecies[i] 의 상한이다
        public EnemyTypeId[] CapSpecies = Array.Empty<EnemyTypeId>();
        public int[] Cap = Array.Empty<int>();

        // ── 좁은 들: 전장 가장자리 띠의 가시밭(spec 07 9절 [임시 세부], TV-107) ──
        // ThornBorderWidth == 0 이면 효과 없음(나머지 셋도 0). 좁은 들: 바깥 테두리에서 안쪽 2칸(Raw 8,589,934,592),
        // WavePrelude 예고 180틱(3초), 같은 가시밭에서 90틱(1.5초)에 1회 피해 1칸. 적 스폰 위치는 띠 바로 안쪽으로 옮긴다
        public Fix64 ThornBorderWidth;
        public int ThornTelegraphTicks;
        public int ThornContactIntervalTicks;
        public int ThornDamage;                             // 체력 칸
    }
}
