using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 05 2.3(필드)·4절(종별 공격 표). EnemyDef.Attacks 의 한 줄. 원본 data/sim/enemies.json 의 attacks.
    // 단위: 거리·반지름·폭·길이는 칸 Fix64, 각도는 센티도(CONTRACTS 0.3), 시간은 틱, 이동 속도는 센티 칸/초, 탄속은 틱당 Fix64.
    // spec 05 2.3 에서 null 일 수 있는 칸(shape, length, range, trigger_min·max, proj, escape_override_milli)은 null 로 둔다.
    // spec 05 2.3 의 id(golem_menhir.slam 꼴)는 화면에 보이지 않고 재실행에 쓰이지 않아 팩에 없다. 공격은 EnemyDef.Attacks 의 첨자로 가리킨다.
    // leaves_hazard(HazardDef)는 판정 뒤 남는 위험 구역(가시덩굴 씨앗의 가시밭, spec 05 4.10)이다. 남기지 않으면 null.
    // 이 타입이 쓰는 열거형과 탄·남는 구역 정의(ProjDef, HazardDef)는 이 타입 안에 둔다(소유 파일 하나, 공개 최상위 타입 하나).
    public sealed class AttackDef
    {
        public AttackKind Kind;
        public TelegraphShape? Shape;                       // 예고 모양. 예고가 없는 공격(접촉, 시전만 하는 탄)은 null
        public Fix64 Radius;                                // Circle·Fan 반지름, Ring 바깥 반지름
        public Fix64 InnerRadius;                           // Ring 전용
        public Fix64 Width;                                 // Band·AimLine 폭
        public Fix64? Length;                               // Band 길이. null = 벽까지(레이캐스트)
        public int FanCenti;                                // 부채꼴 전체 각도(110도 = 11000)
        public Fix64? Range;                                // 발동 사거리(시작 조건). null = 조건 없음
        public Fix64? TriggerMin, TriggerMax;               // 거리 조건(늑대 3.5~5.5). null = 그쪽 끝이 열려 있다
        public int CastTicks;                               // 구역 없는 시전 동작(술사 0.4초 = 24). 동시 예고 상한에 세지 않는다(spec 05 U-12)
        public int TelegraphTicks;                          // 예고 길이 N(spec 05 3.3). R1 검사 대상. 예고가 없으면 0
        public LockMode Lock;                               // 방향 고정 방식
        public int LockTicks;                               // LastNTicks 의 n. AtStart 이면 0
        public AnchorMode Anchor;                           // 구역 위치를 정하는 방법
        public int ActiveTicks;                             // 판정 뒤 움직이는 시간(늑대 돌진 6칸 ÷ 18칸/초 = 20틱)
        public int DashSpeedCenti;                          // 돌진 속도, 센티 칸/초(18 → 1800). 돌진이 아니면 0. 북두꺼비 버프를 받지 않는다(U-21)
        public int RecoveryTicks;                           // 빈틈(늑대는 경직). 북두꺼비 버프 −30% 대상(spec 05 3.8)
        public int Damage;                                  // 종별 피해(칸). 웨이브 보정은 spec 05 3.7(접촉·탄은 늘 1)
        public DamageSource Source;
        public ProjDef? Proj;                               // 쏘는 탄. 없으면 null
        public bool Grazeable;                              // 스쳐 피하기 대상(spec 05 3.10)
        public bool IsDash;                                 // 돌진(설치 벽 규칙, spec 05 2.3)
        public bool Lob;                                    // 포물선(설치 벽을 넘는다)
        public int? EscapeOverrideMilli;                    // 검산용 벗어나는 거리(milli 칸). null 이면 모양에서 계산(spec 05 10.1)
        public int CooldownTicks;                           // 재사용 대기 [임시: spec 05 U-05]
        public HazardDef? LeavesHazard;                     // 판정 뒤 남는 위험 구역(가시밭). 없으면 null(spec 05 2.3 leaves_hazard)

        // spec 05 2.3 kind 의 나열 그대로(값은 선언 순서, 0부터)
        public enum AttackKind : byte
        {
            Contact = 0,
            Dash = 1,
            Zone = 2,
            AimedShot = 3,
            Spread = 4,
            RingShot = 5,
            Lob = 6,
            SelfDestruct = 7,
            Tongue = 8,
        }

        // spec 05 2.1(다섯 가지 고정)
        public enum TelegraphShape : byte
        {
            Circle = 0,
            Band = 1,
            Fan = 2,
            AimLine = 3,
            Ring = 4,
        }

        // spec 05 2.1. 늑대는 AtStart, 고슴도치는 LastNTicks(spec 05 3.3)
        public enum LockMode : byte
        {
            AtStart = 0,
            LastNTicks = 1,
        }

        // spec 05 2.1
        public enum AnchorMode : byte
        {
            Self = 0,
            PlayerPosAtStart = 1,
            PlayerPosLeadAhead = 2,
            AlongLineToWall = 3,
        }

        // spec 05 2.1(M 4.2 분류). 접촉·탄·남는 구역은 늘 1칸, 예고 공격은 종별 값(spec 05 3.7)
        public enum DamageSource : byte
        {
            Contact = 0,
            Projectile = 1,
            Telegraph = 2,
            Lingering = 3,
        }

        // spec 05 2.3 proj. 적 탄의 속성은 늘 무속성이라 칸이 없다(spec 05 1.3 A1·A3)
        public sealed class ProjDef
        {
            public Fix64 SpeedPerTick;                      // 탄속(칸/초 ÷ 60). 6.5 → 465,288,124 Raw
            public int Count;                               // 한 번에 쏘는 탄 수
            public int SpreadCenti;                         // 이웃한 탄 사이 각도(술사 20도 = 2000). 한 발이면 0
            public Fix64 Range;                             // 사거리(칸). 0 이면 사거리 제한 없이 벽에 닿을 때까지(고슴도치 가시, spec 05 4.6)
            public Fix64 HitRadius;                         // 탄 판정 반지름(칸)
        }

        // spec 05 2.1 HazardKind(선언 순서 그대로, 0부터). 서리밭은 보스 데이터가 쓴다
        public enum HazardKind : byte
        {
            ThornField = 0,
            EmberStrip = 1,
            SporeCloud = 2,
            FrostField = 3,
        }

        // spec 05 2.3 leaves_hazard, 2.4 LingeringHazard, 3.6(접촉 규칙). 남는 구역의 피해는 늘 1칸(spec 05 3.7)이라 칸이 없다
        public sealed class HazardDef
        {
            public HazardKind Kind;
            public Fix64 Radius;                            // 반지름(칸). 가시밭 1.5
            public int DurationTicks;                       // 남는 시간. 가시밭 4초 = 240, 꽃 핀 가시덩굴 6초 = 360
            public int MaxCount;                            // 전장에 동시에 있을 수 있는 수. 넘으면 SpawnTick 이 가장 작은 것부터 지운다(가시밭 3)
            public int ContactCooldownTicks;                // 같은 구역에서 다시 피해를 받기까지(1.5초 = 90, 구역 Id 별, U-16)
        }
    }
}
