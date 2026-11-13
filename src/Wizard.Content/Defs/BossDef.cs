using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 06 1.3(BossDef)·1.4(PhaseDef)·2절(공통 규칙)·3~8절(보스별). 원본 data/sim/bosses.json, 절 꼴은 Pack/Sections/ContentDb.Bosses.cs.
    // 판 안에서 바뀌는 값(hpMax = BaseHp × H(w) ÷ H(HpScaleAnchorWave), 격노 스택, 단계, 2회차 여부)은 여기 없다. 이것은 보스의 바탕 값이다.
    // 그림 쪽 값(캔버스, 팔레트), 문자열(이름 띠는 이름 표의 boss.<키>), 12절 격노 연출의 크기·색은 재실행에 쓰이지 않아 이 절(코어 버전 해시 안)에 두지 않는다.
    // 피해 성장(웨이브 31부터 예고 공격 +1)과 한 번 피해 상한 3칸은 WaveCurveDef(waves.json 의 damage)가 가진다(spec 06 2.7).
    // 처음 꼬리표(spec 06 1.3 initialTags)는 Phases[0].Tags 다. 단계가 바뀌면 그 단계의 Tags 로 바꾼다(천 갓의 뿌리, spec 06 5.3).
    // 이 타입이 쓰는 묶음(공통 규칙, 단계, 덮어쓰기, 허상의 복사 규칙, 첫 만남, 여러 부위 몸)은 이 타입 안에 둔다(소유 파일 하나, 공개 최상위 타입 하나).
    public sealed class BossDef
    {
        public BossId Id;
        public int RotationIndex;                           // 0 가마등, 1 허상, 2 천 갓, 3 밤눈, 4 흰뿔, 5 밤삼킴(spec 06 1.3, spec 14 3.8)
        public int BaseHp;                                  // 웨이브 10 기준 체력
        public int HpScaleAnchorWave;                       // 체력 배율의 기준 웨이브(기본 10, 허상의 대안 20, spec 06 2.2 [임시])
        public int MoveSpeedCenti;                          // 이동 속도, 센티 칸/초(2.2 → 220). R2: V 미만
        public int? MoveSpeedOnFrostCenti;                  // 플레이어가 서리밭 안일 때 걷는 속도(흰뿔 2.4 → 240). null 이면 서리밭과 상관없다
        public int ContactDamage;                           // 몸 접촉 피해(칸). 웨이브 성장을 받지 않는다(spec 06 15절 C3)
        public Fix64 BodyRadius;                            // 몸 판정 반지름(칸). 밤삼킴은 머리(Body 가 부위별 값을 가진다) [임시: 밤삼킴 밖은 원문 없음]
        public Element Element;                             // 자기 속성. 천 갓은 None
        public Element Weak, Resist;                        // 약점 ×1.25, 저항 ×0.8. 둘은 다르고 None 이 아니다

        public MovementDef Movement = new MovementDef();
        public PhaseDef[] Phases = Array.Empty<PhaseDef>();                         // 1단계부터 차례로(EnterAtHpPercent 내림차순)
        public BossPatternDef[] Patterns = Array.Empty<BossPatternDef>();          // PatternKey 오름차순(패턴과 상시 행동)
        public PatternOverride[] SecondCycle = Array.Empty<PatternOverride>();    // 2회차(웨이브 70 이상) 강화(spec 06 2.6). 3회차 이상도 같다(U12)
        public PhantomCopyDef? PhantomCopy;                 // 허상만. 플레이어 지팡이의 복사 규칙(spec 06 4.4)
        public FirstEncounterDef? FirstEncounter;           // 허상만. 프로필의 첫 웨이브 10(spec 06 9절)
        public BodyDef? Body;                               // 밤삼킴만. 머리·마디·꼬리(spec 06 8.1)

        // 모든 보스가 같은 인스턴스를 가리킨다(절에 한 번 들어 있다)
        public CommonDef Common = new CommonDef();

        // Patterns 에서 열쇠로 찾는다. 없으면 null
        public BossPatternDef? FindPattern(BossPatternDef.PatternKey key)
        {
            foreach (BossPatternDef p in Patterns)
            {
                if (p.Key == key)
                {
                    return p;
                }
            }
            return null;
        }

        // spec 06 2.3·2.4·2.8·2.11·2.12. 시간은 틱
        public sealed class CommonDef
        {
            public int IntroTicks;                          // 등장 1.2초 = 72. 이 동안 보스는 공격하지 않고 격노 시계가 돌지 않는다
            public int PhaseTransitionTicks;                // 단계 전환 0.6초 = 36. 이 동안 보스는 공격하지 않는다
            public int WaitTicks;                           // 패턴 사이 기본 대기 1.5초 = 90
            public int NoCandidateWaitTicks;                // 고를 패턴이 없으면 대기를 이만큼 늘린다(30 [임시])
            public int R5RetryTicks;                        // R5 막힘 재검사 간격(3 [임시: U8])
            public int R5RepickTicks;                       // R5 막힘이 이보다 길면 같은 단계의 다른 패턴(120 [임시: U8])
            public int RageStackTicks;                      // 격노 1스택마다 20초 = 1200(등장이 끝난 틱부터)
            public int RageMaxStacks;                       // 5
            public int RageStepPermille;                    // 스택당 대기·연사 간격 −10% = 100. 값 × (1000 − 100 × n) ÷ 1000 내림(U2)
            public int FinalRageTicks;                      // 최종 격노 180초 = 10800
            public int FinalRageDamagePlus;                 // 최종 격노 뒤 모든 공격 +1칸(상한은 WaveCurveDef). 스침 점수 정지
            public int ActionHaltTicks;                     // ⑨ 뇌광창 기절 대신 0.3초 = 18 동작 멈춤
            public int ContactCooldownTicks;                // 같은 보스에게서 접촉 피해 1.5초 = 90 에 1회(밤삼킴의 부위는 같은 적)
        }

        // 이동 AI 의 거리. 쓰지 않는 보스는 0
        public sealed class MovementDef
        {
            public Fix64 KeepDistance;                      // 플레이어와 유지하는 거리(허상 5칸 [임시: U14])
            public Fix64 WallMargin;                        // 벽에서 이만큼 안쪽(허상 1.5칸 [임시: U14])
        }

        // spec 06 1.4
        public sealed class PhaseDef
        {
            public int Index;                               // 1~3
            public int EnterAtHpPercent;                    // hp × 100 ≤ hpMax × 이 값 이 되면 들어간다. 1단계는 100
            public EnemyTag Tags;                           // 이 단계의 꼬리표(늘 Boss 포함). 밤눈의 착지 중 비행 떼기처럼 상태에 따른 것은 두뇌가 덮는다
            public Fix64 AnchorRadius;                      // 이 단계 동안 전장 중앙에서 이 반지름 안에서만 움직인다(천 갓 1단계 1칸 [임시: U60]). 0 = 제한 없음
            public BossPatternDef.PatternKey[] Patterns = Array.Empty<BossPatternDef.PatternKey>();  // 고를 수 있는 패턴
            public BossPatternDef.PatternKey[] Passives = Array.Empty<BossPatternDef.PatternKey>();  // 패턴 고르기와 별개로 도는 상시 행동
            public PatternOverride[] Overrides = Array.Empty<PatternOverride>();                     // 이 단계의 수치 변화
        }

        // 패턴 단계 하나의 수치 바꾸기(spec 06 1.4 overrides, 2.6 2회차). 단계가 바뀌거나 2회차일 때 원래 값 대신 Value 를 쓴다.
        // 단계 덮어쓰기와 2회차 덮어쓰기가 같은 칸이면 2회차가 이긴다(지금 데이터에는 겹치는 칸이 없다)
        public sealed class PatternOverride
        {
            public BossPatternDef.PatternKey Pattern;
            public int Step;                                // BossPatternDef.Steps 의 첨자
            public OverrideKey Key;
            public long Value;                              // 단위는 Key 가 정한다
        }

        // 덮어쓰는 칸(값은 선언 순서, 0부터). 값의 단위: 이름 끝의 단위(Ticks 틱, Centi 센티도, Raw Q32.32 Raw), 나머지는 정수
        public enum OverrideKey : byte
        {
            Repeat = 0,                                     // StepDef.Repeat
            Count = 1,                                      // StepDef.Count
            RepeatIntervalTicks = 2,                        // StepDef.RepeatIntervalTicks
            RepeatAngleCenti = 3,                           // StepDef.RepeatAngleCenti
            RadiusRaw = 4,                                  // StepDef.Attack.Radius
            InnerRadiusRaw = 5,                             // StepDef.Attack.InnerRadius
            SweepCenti = 6,                                 // StepDef.Stream.SweepCenti
            ShardCount = 7,                                 // StepDef.Obstacle.Shards.Count
            ShardSpreadCenti = 8,                           // StepDef.Obstacle.Shards.SpreadCenti
            HazardDriftPerTickRaw = 9,                      // StepDef.HazardDriftPerTick
        }

        // spec 06 4.2·4.4 TransformForPhantom. 복사한 투사체 모두에 건다
        public sealed class PhantomCopyDef
        {
            public int IntervalPermille;                    // 발사 간격 = 플레이어 간격 × 2.5(2500, 올림 U19). 격노·하한은 copy_fire 단계의 연사형 칸
            public int MaxProjectiles;                      // 발사 1회 최대 12개(플레이어의 64개 상한 대신)
            public Fix64 SpeedPerTick;                      // 속도 6칸/초(틱당 Raw)
            public int Damage;                              // 1
            public int BounceCap;                           // 벽 튕김 1회까지
            public int ExplosionRadiusPermille;             // 폭발 반경 = 플레이어의 60%(600)
            public Fix64 ExplosionRadiusMax;                // 최대 1.5칸
            public int ExplosionTelegraphTicks;             // 멈춘 자리에 0.8초 = 48 예고 원 뒤 터진다
            public int ExplosionDamage;                     // 1 [임시: U18]
            public int ChainLineTelegraphTicks;             // 연쇄 번개 → 조준 예고선 0.6초 = 36 뒤 직선 번개
            public Fix64 ChainLineWidth;                    // 0.5 [임시: U17]
            public Fix64 ChainLineLength;                   // 7 [임시: U17]
            public int SplitMinAngleCenti;                  // 분열 갈래 사이 최소 각 1/3 rad = 19.099도 → 1910(올림)
            public int CopyMutationsFromCycle;              // 변이는 2회차(웨이브 80 이상)부터 복사한다(spec 06 2.6)
        }

        // spec 06 9.2·9.5·9.6, 12절. 시각은 등장이 끝난 틱(fightTicks 0)부터, 되비침·쓰러짐은 그 상태에 들어온 틱부터
        public sealed class FirstEncounterDef
        {
            public int HpFloorPercent;                      // 체력 바닥 hpMax × 50 ÷ 100
            public int MaxFightTicks;                       // 싸움 최대 45초 = 2700
            public int RageStacks;                          // 처음부터 5스택(최종 격노 없음)
            public int StartPhase;                          // 처음부터 3단계 행동 전부(단계 전환 없음, 졸개 없음)
            public int FinisherTicks;                       // 마무리 되비침 3.0초 = 180(입력 무시)
            public int FinisherHpZeroTick;                  // 되비침 시작 +114틱에 플레이어 체력 0(15절 C12)
            public int CollapseTicks;                       // 쓰러짐 4.0초 = 240 뒤 일어남
        }

        // spec 06 8.1(밤삼킴). 머리와 마디·꼬리는 체력을 함께 쓴다
        public sealed class BodyDef
        {
            public Fix64 HeadRadius;                        // 0.75 [임시: U40]
            public Fix64 SegmentRadius;                     // 0.5 [임시: U40]
            public Fix64 TailRadius;                        // 0.5 [임시: U40]
            public int SegmentCount;                        // 몸 마디 10개(꼬리 빼고)
            public Fix64 SegmentSpacing;                    // 1.0칸(경로 따라가기) [임시: U40]
            public int SegmentDamagePermille;               // 마디 ×0.5 = 500
            public int TailDamagePermille;                  // 꼬리 = 마디와 같음 [임시: U40]
            public int MaxSegmentHits;                      // 탄 한 발이 피해를 주는 마디·꼬리 3개까지(머리는 상한과 무관)
        }
    }
}
