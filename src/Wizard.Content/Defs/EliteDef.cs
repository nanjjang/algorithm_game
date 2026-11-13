using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2(ContentDb.Elite(바탕 종)), spec 05 7절(엘리트 12종과 고유 능력), spec 14 3.7(id elite.<바탕 키>, 코드 200 + 종).
    // 원본 data/sim/elites.json, 절 꼴은 Pack/Sections/ContentDb.Elites.cs.
    // 엘리트는 종이 아니라 바탕 종(EnemyDef)의 개체 상태다(EnemyState.IsElite). 바탕 종의 수치·공격·약점·저항·꼬리표를 그대로 쓰고
    // 꼬리표 Elite 가 더해진다(spec 05 7.1, 1.3 A4). 체력 ×3·비용 ×3 은 웨이브 곡선(WaveCurveDef.EliteHpMul·EliteCostMul)이 가진다.
    // 이 타입은 엘리트에만 있는 것(고유 능력 하나와 그 매개변수, 능력이 쓰는 공격)만 가진다.
    // 머리 장식 그림 크기(head_ornament_px)는 그림 쪽 값이라 이 절(코어 버전 해시 안)에 두지 않는다.
    public sealed class EliteDef
    {
        public EnemyTypeId BaseType;
        public EliteAbility Ability;

        // 고유 능력이 쓰는 공격(spec 05 7.2·7.3). 바탕 공격을 대신하는 것, 바탕 공격 뒤에 이어 나오는 것, 따로 쓰는 것이 있다(EliteAttack)
        public EliteAttack[] Attacks = Array.Empty<EliteAttack>();

        public AbilityParams Params = new AbilityParams();

        // 종마다 하나(spec 05 7.1). 값은 spec 05 7.2 표의 줄 순서(0부터)
        public enum EliteAbility : byte
        {
            Split = 0,              // 왕이끼 슬라임: 죽으면 3마리로 갈라진다
            Howl = 1,               // 우두머리 늑대: 울부짖음 뒤 주위 늑대가 차례로 돌진, 자신은 돌진 2회
            Shards = 2,             // 고인돌지기: 내려찍기 자리에서 돌 파편 고리, 철갑 160도
            WideSpread = 3,         // 큰갓 술사: 5갈래 탄
            BigBlast = 4,           // 왕꽈리 벌레: 폭발 반경 2.6, 예고 1.1초, 유폭 60
            TripleAim = 5,          // 삼연 고슴도치: 조준선 3개를 차례로
            EarthBands = 6,         // 화분 어미: 솟은 자리에서 흙 띠 네 방향
            Summon = 7,             // 박쥐 여왕: 체력 문턱마다 박쥐를 부르고 급강하
            TwinRing = 8,           // 쌍둥이불: 고리 두 번, 두 번째는 어긋나게
            Bloom = 9,              // 꽃 핀 가시덩굴: 씨앗 2개, 가시밭 6초
            Bounce = 10,            // 외엄니: 벽에서 한 번 튕겨 다시 돌진
            BigDrum = 11,           // 큰북 두꺼비: 울음 반경 6, 끊기 60, 빙결·기절로 취소되지 않음
        }

        // 엘리트 공격 하나. ReplacesIndex·FollowsIndex 는 바탕 종 EnemyDef.Attacks 의 첨자다(없으면 −1, 둘이 같이 있지 않다)
        public sealed class EliteAttack
        {
            public int ReplacesIndex = -1;                  // ≥ 0: 바탕 공격 그 첨자 대신 이 공격을 쓴다(큰갓 5갈래, 왕꽈리 점화, 꽃 핀 씨앗)
            public int FollowsIndex = -1;                   // ≥ 0: 바탕 공격 그 첨자의 판정 틱에 그 구역(원의 중심, 솟은 자리)에서 이어 나온다(고인돌 파편, 화분 어미 흙 띠)
            public AttackDef Attack = new AttackDef();      // 둘 다 −1 이면 따로 쓰는 공격이다(박쥐 여왕 급강하)
        }

        // 고유 능력의 매개변수(spec 05 7.2 표, 7.3 의사코드). 그 능력이 쓰지 않는 값은 0
        public sealed class AbilityParams
        {
            // Split(왕이끼 슬라임): 사망 위치 + 반지름 0.5 의 120도 간격(시작각 0도)에 바탕 종 3마리(엘리트 아님, 보상 없음) [임시: U-69]
            public int SplitCount;
            public Fix64 SplitRadius;
            public int SplitStartAngleCenti;

            // Howl(우두머리 늑대): 울부짖음 1.0초 = 60 뒤 반경 6 안의 늑대에게 0.25초 = 15 간격으로 돌진 요청.
            // 자신은 돌진을 2회 잇는다(첫 돌진 뒤 경직 없이 둘째 예고, 사이 0틱) [임시: U-71]
            public int HowlTicks;
            public Fix64 HowlRadius;
            public int HowlStepTicks;
            public int ChainCount;
            public int ChainGapTicks;

            // Shards(고인돌지기): 철갑 반각 80도(정면 160도). 파편은 Attacks 의 이어 나오는 공격이다
            public int ArmorHalfAngleCenti;

            // BigBlast(왕꽈리 벌레): 유폭이 적에게 60(× H(w)). 지연은 바탕 종과 같은 24틱
            public Fix64 SympatheticDamage;

            // TripleAim(삼연 고슴도치): 조준선 3개를 0.5초 = 30 간격으로 시작(각각 바탕 조준과 같은 60틱, 고정 24틱). 각자 요청 [임시: U-73]
            public int AimCount;
            public int AimStepTicks;

            // EarthBands(화분 어미): 흙 띠 4개, 첫 방향 0도(+x)에서 360 ÷ 수 간격(±x, ±y) [임시: U-74]. 한 묶음(전부 아니면 전무)
            public int BandCount;
            public int BandStartAngleCenti;

            // Summon(박쥐 여왕): 체력 비율이 각 문턱 아래로 처음 내려간 틱에 4마리(보상 없음), 여왕 주위 반지름 1 의 360 ÷ 수 간격 [임시: U-75]
            public int SummonCount;
            public Fix64 SummonRadius;
            public int[] SummonHpPermille = Array.Empty<int>();     // 내림차순(600, 200)

            // TwinRing(쌍둥이불): 고리를 2번, 0.5초 = 30 간격, 두 번째는 22.5도 = 2250 어긋나게
            public int RingRepeat;
            public int RingRepeatStepTicks;
            public int RingRepeatAngleCenti;

            // Bloom(꽃 핀 가시덩굴): 씨앗 2개(플레이어 위치, 이동 방향 3칸 앞 [임시: U-77]), 전장 밖이면 벽 안 0.5칸으로 자른다 [임시]
            public int SeedCount;
            public Fix64 SeedLeadDistance;
            public Fix64 SeedWallInset;

            // Bounce(외엄니): 첫 전장 벽 충돌에서 1번 튕긴다. 튕김 전 0.5초 = 30 멈춤, 새 띠 예고는 바탕 돌진과 같다(54틱) [임시: U-78]
            public int BounceCount;
            public int BounceStopTicks;

            // BigDrum(큰북 두꺼비): 울음 반경 6, 끊기 누적 피해 60(× H(w)), 빙결·기절로는 취소되지 않는다(spec 05 6.3 X9)
            public Fix64 CroakRadius;
            public Fix64 CroakBreakDamage;
            public bool CroakBreakOnly;
        }
    }
}
