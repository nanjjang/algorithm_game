using System;
using Wizard.Core;

namespace Wizard.Content
{
    // spec 06 1.5(PatternDef)·3~8절의 패턴 표. BossDef.Patterns 의 한 줄. 원본 data/sim/bosses.json 의 patterns, 절 꼴은 Pack/Sections/ContentDb.Bosses.cs.
    // 패턴은 차례로 도는 단계(Steps)다. 다음 단계는 앞 단계의 판정 틱(반복이 있으면 마지막 반복의 판정 틱)에 StartDelayTicks 만큼 늦게 시작한다.
    // 단계 하나는 공격(AttackDef, 적과 같은 꼴), 복사 발사(허상의 트리 실행), 졸개 부르기 가운데 하나다.
    // 단위: 거리·반지름은 칸 Fix64, 각도는 센티도(CONTRACTS 0.3), 시간은 틱, 비율은 천분율, 탄속은 틱당 Fix64.
    // 예고 시간(AttackDef.TelegraphTicks, FrostTelegraphTicks, 경고 틱)은 격노로 줄지 않는다(spec 06 2.12). 줄어드는 것은 대기와 연사형 간격뿐이다.
    // spec 06 1.5 의 id(boar_kiln.triple_charge 꼴)는 화면에 보이지 않고 재실행에 쓰이지 않는다. 팩에는 PatternKey 번호만 있다.
    // 보스별 고르기 규칙(1.5 eligibility: 흰뿔의 숨결 금지·기둥 강제, 구름·새싹·기둥 상한)은 데이터가 아니라 보스 두뇌의 규칙이다(spec 06 7.4, U31·U32·U36).
    // 이 타입이 쓰는 열거형과 단계 정의는 이 타입 안에 둔다(소유 파일 하나, 공개 최상위 타입 하나).
    public sealed class BossPatternDef
    {
        public PatternKey Key;
        public bool IsPassive;                              // 상시 행동(spec 06 1.4 passiveIds): 패턴 고르기와 별개로 돈다(허상 복사 사격·졸개 부르기, 밤삼킴 비늘 탄)
        public GrazeMask Graze;                             // 패턴 표의 "스침" 열
        public int RecoveryTicks;                           // 패턴 뒤 빈틈(보스가 공격하지 않는 시간). 없음 = 0
        public int RecoveryDamagePermille;                  // 빈틈 동안 받는 피해 배율(가마등 기절 ×1.5 = 1500). 그 밖 1000
        public StepDef[] Steps = Array.Empty<StepDef>();

        // spec 06 3~8절 패턴 표의 id(값은 추가만 한다. 보스 순번과 표의 줄 순서)
        public enum PatternKey : byte
        {
            None = 0,
            // 가마등(spec 06 3.2)
            TripleCharge = 1,
            Stomp = 2,
            Rockfall = 3,
            // 스승의 허상(spec 06 4.3)
            CopyFire = 4,
            SummonSlimes = 5,
            MimicArt = 6,
            BloomMimic = 7,
            // 천 갓(spec 06 5.2)
            SporeRing = 8,
            SporeCloud = 9,
            SummonSprouts = 10,
            Leap = 11,
            // 밤눈(spec 06 6.2)
            Glide = 12,
            FeatherFan = 13,
            LightningStrike = 14,
            LightningPlow = 15,
            // 흰뿔(spec 06 7.2)
            IcePillar = 16,
            HornSwing = 17,
            FrostBreath = 18,
            HornCharge = 19,
            FrostField = 20,
            // 밤삼킴(spec 06 8.2)
            Coil = 21,
            DarkSpit = 22,
            Burrow = 23,
            ScaleVolley = 24,
        }

        // spec 06 1.2 GrazeKind(None 빼고). 비트 = 1 << (GrazeKind − 1)
        [Flags]
        public enum GrazeMask : byte
        {
            None = 0,
            Bullet = 1,
            Charge = 2,
            Zone = 4,
        }

        // 단계가 하는 일(값은 선언 순서, 0부터)
        public enum StepKind : byte
        {
            Attack = 0,                                     // AttackDef 하나(예고 구역, 돌진, 탄)
            Copy = 1,                                       // 플레이어 지팡이 트리의 복사 발사(허상, spec 06 4.4). 변환 규칙은 BossDef.PhantomCopy
            Summon = 2,                                     // 졸개 부르기
        }

        // 판정 뒤 남는 장애물(값은 선언 순서, 0부터). spec 06 1.1 의 obstacle.boulder, obstacle.ice_pillar
        public enum ObstacleKind : byte
        {
            Boulder = 0,
            IcePillar = 1,
        }

        // 패턴의 단계 하나. 적지 않은 칸은 0, false, null 이다(Count 와 Repeat 는 1)
        public sealed class StepDef
        {
            public StepKind Kind;
            public AttackDef? Attack;                       // Kind == Attack 일 때만. 보스 공격의 빈틈·재사용 대기는 0 이다(패턴의 RecoveryTicks, 공통 대기를 쓴다)
            public CopyDef? Copy;                           // Kind == Copy 일 때만
            public SummonDef? Summon;                       // Kind == Summon 일 때만

            public int Count = 1;                           // 한 번에 까는 구역·띠·마디 탄의 수(낙석 6, 기둥 4, 밭갈이 줄 7, 십자 활강 2, 비늘 탄 마디 10)
            public int Repeat = 1;                          // 이 단계를 하는 횟수(삼연 돌진의 2·3번째 2, 낙뢰 4). 0 = 상시 행동이 이어서 계속
            public int RepeatIntervalTicks;                 // 반복의 시작 사이(낙뢰 0.4초 = 24). 0 = 앞 반복이 끝난 틱에(돌진이 벽에 닿은 틱, 솟은 틱)
            public int RepeatAngleCenti;                    // 반복마다 어긋나는 각(포자 고리 11.25도 = 1125)
            public bool TelegraphEachRepeat;                // 반복마다 예고를 다시 보인다. false 면 첫 반복 앞에 한 번(cast·경고 포함)
            public int StartDelayTicks;                     // 앞 단계 판정 뒤 이 단계 시작까지(밭갈이 둘째 묶음 0.8초 = 48)
            public bool RapidFire;                          // 연사형(spec 06 2.12): 격노로 반복 간격(줄기는 줄기 간격)이 스택당 −10%
            public int MinIntervalTicks;                    // 연사형 간격의 R3 하한(spec 06 2.12 표, U4). 연사형이 아니면 0
            public int FrostTelegraphTicks;                 // 예고 시작 틱에 플레이어 피격 원 중심이 서리밭 안이면 이 예고를 쓴다(흰뿔 휘두르기 1.2초 = 72). 0 = 같다
            public Fix64 ClearHazardMargin;                 // 새 예고가 시작되면 예고 띠 양옆 이 거리 안에 걸친 자기 남는 구역을 지운다(불씨 띠 1.5칸, spec 06 3.4, U30)
            public int HazardSpeedPermille;                 // 남는 구역 안의 플레이어 이동 속도 배율(서리밭 −30% = 700). 0 = 바꾸지 않는다
            public Fix64 HazardDriftPerTick;                // 남는 구역이 플레이어 쪽으로 흐르는 속도(틱당. 천 갓 2회차 0.8칸/초). 0 = 흐르지 않는다
            public StreamDef? Stream;                       // 쓸어 가며 이어 쏘는 줄기(흰뿔 숨결). 없으면 null
            public PlacementDef? Placement;                 // 여러 구역의 자리(낙석, 포자 구름, 기둥, 서리밭). 없으면 Attack.Anchor 하나
            public ObstacleDef? Obstacle;                   // 판정 뒤 남는 장애물(바위, 얼음 기둥). 없으면 null
        }

        // 쓸어 가며 이어 쏘는 탄 줄기(spec 06 7.2·7.5). 줄기의 탄은 Attack.Proj
        public sealed class StreamDef
        {
            public int IntervalTicks;                       // 줄기 간격(초당 4발 = 15틱). 연사형이면 격노로 줄고 MinIntervalTicks 에서 멈춘다
            public int SweepCenti;                          // 쓸어 가는 전체 각(90도 = 9000). 쓰는 틱 수 = SweepCenti ÷ SweepCentiPerTick
            public int SweepCentiPerTick;                   // 틱당 도는 각(초당 45도 = 75 센티도/틱)
        }

        // 여러 구역을 놓는 자리(spec 06 3.4 낙석 U28, 5.4 포자 구름 U31, 7.7 기둥·서리밭 U39). 거부 표본 추출은 rng.boss 로 한다
        public sealed class PlacementDef
        {
            public int AtPlayer;                            // 그 틱의 플레이어 위치에 놓는 수(나머지는 무작위)
            public Fix64 MinPlayerDistance;                 // 무작위 구역과 플레이어 사이 중심 거리 아래 끝
            public Fix64? MaxPlayerDistance;                // 위 끝. null = 없음(전장 안 어디든)
            public Fix64 MinSpacing;                        // 구역끼리 중심 거리 아래 끝
            public Fix64 WallMargin;                        // 벽에서 중심까지 아래 끝
            public int MaxTries;                            // 구역 하나당 시도 수. 넘으면 그 구역은 생략한다
        }

        // 판정 뒤 남는 장애물(spec 06 3.4 바위, 7.7 얼음 기둥). R5 는 지나갈 수 없는 칸으로 본다
        public sealed class ObstacleDef
        {
            public ObstacleKind Kind;
            public Fix64 Radius;                            // 판정 반지름(바위 0.5 [임시: U29], 기둥 1.0 [임시: U38])
            public int DurationTicks;                       // 남는 시간(바위 4초 = 240). 0 = 깨질 때까지
            public int Hp;                                  // 체력(웨이브 배율 없음, 기둥 80). 0 = 탄으로 깨지지 않는다
            public int MaxCount;                            // 전장에 동시에 있을 수 있는 수(기둥 6). 넘는 만큼 새것을 생략한다. 0 = 없음
            public bool BlocksPlayerShots, BlocksEnemyShots;
            public AttackDef.ProjDef? Shards;               // 깨질 때 둘레로 퍼지는 파편(바위 6방향, 기둥 8방향). 피해 1(탄). 없으면 null
        }

        // 졸개 부르기(spec 06 2.5, 4.2, 5.2). 졸개는 꼬리표·상성·점수·골드·드랍이 없고 체력에 H(w)를 곱한다(U10)
        public sealed class SummonDef
        {
            public EnemyTypeId Enemy;                       // 부르는 적(허상은 enemy.slime_moss 를 졸개로, 천 갓은 enemy.spore_seedling)
            public int Count;                               // 한 번에 부르는 수
            public int FieldCap;                            // 전장 동시 상한(새싹 8). 넘는 만큼 생략, 0마리면 고를 수 없다(U32). 0 = 없음
            public int WarnTicks;                           // 나오기 전 땅의 금(1.0초 = 60). 0 = 없음
        }

        // 플레이어 지팡이 트리의 복사 발사(spec 06 4.3·4.4). 탄은 BossDef.PhantomCopy 로 바꾼다
        public sealed class CopyDef
        {
            public ArtId[] Arts = Array.Empty<ArtId>();     // 따라 쓸 비기 후보(ArtId 오름차순). 빈 배열 = 트리 1회(복사 사격). 첫 만남은 해금과 무관하게 전부
            public ArtId Fallback;                          // 후보를 하나도 해금하지 않았을 때 쓰는 비기(① 마력 방출). None = 없음
            public int WarnTicks;                           // 쓰기 전 예고(커맨드 화살표 0.8초 = 48, 발밑 8방향 선 1.2초 = 72). 0 = 없음
        }
    }
}
