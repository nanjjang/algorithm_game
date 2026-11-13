using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 03 12.1: Id, Kind, Owner, Paths, IsJust, Difficulty, CostKind, Cost, MinCost, CreditAllowed, FallbackParent,
    // Unlock, Effect, Duel. 필드는 명세의 표가 주인이고 비기 데이터 작업(M5-DAT-01)이 채운다. 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/arts.json, 절 꼴은 Pack/Sections/ContentDb.Arts.cs.
    //
    // 명세의 이름과 다른 것(CONTRACTS 2.1 정본, 계층):
    //   - spec 03 12.1 의 NameKey 는 두지 않는다. 이름은 이름 표의 열쇠 = ContentIds.ToId(Id) 다(CLAUDE.md 5절).
    //   - Owner 는 CharacterId? 대신 CharacterId. 공용은 Baseline(0)이다. FallbackParent 는 ArtId? 대신 ArtId. 없으면 None.
    //   - Paths 의 방향은 Wizard.Sim.Arts.CmdDir 과 같은 번호의 CommandStep 이다(Content 는 Sim 을 참조하지 못한다, 의존은 한 방향).
    //   - spec 03 12.1 의 별도 타입(ArtKind, CostKind, UnlockRule, ArtEffectParams, DuelOverride)은 이 타입 안에 둔다.
    //     Duel 은 결투에서 쓰는 효과 값 한 벌 전체다(결투 줄이 없는 칸은 Effect 와 같다, spec 03 16절·14절의 "결투" 줄).
    public sealed class ArtDef
    {
        // spec 03 12.1 ArtKind. 번호는 절 바이트에 들어가므로 더하기만 한다
        public enum ArtKind : byte
        {
            Common = 1,         // 공용 ①~⑧
            Secret = 2,         // 공용 비장 ⑨⑩(CommonSecret)
            Unique = 3,         // 고유(캐릭터 하나)
        }

        // spec 03 12.1 CostKind
        public enum ArtCostKind : byte
        {
            Fixed = 1,          // 결정 Cost 개
            AllCrystals = 2,    // 결정 전부, 최소 MinCost 개(만개, AllMin3)
        }

        // spec 03 2.3 CmdDir 과 같은 번호(Forward 0, Back 1, Left 2, Right 3)
        public enum CommandStep : byte
        {
            Forward = 0,
            Back = 1,
            Left = 2,
            Right = 3,
        }

        // spec 03 15절(U-R2, U-R7, U-R8, 15.3 의 선행 기술과 최소 웨이브)
        public sealed class UnlockRule
        {
            public bool AtStart;                // 시작 해금(마력 방출, 반동 사격, 고유 비기)
            public ArtId Prereq;                // 후보가 되려면 가져야 하는 기술. 없으면 None
            public int MinWave;                 // 이 웨이브 번호의 차례부터 후보(뇌광창 4, 만개 6). 없으면 0
        }

        // spec 03 12.2(공용)·14.1~14.5(고유)의 효과 값. 기술마다 쓰는 칸이 다르고 쓰지 않는 칸은 0이다.
        // 꼬리표별로 줄거나 듣지 않는 것(엘리트 절반, 중량·보스 면역)은 여기 두지 않는다. 그 함수와 값은 spec 04 6절의 몫이다.
        // 뇌광창의 보스 "0.3초 동작 멈춤"만 이 기술의 값이라 BossHaltTicks 에 둔다(spec 03 14.6, A-07).
        public sealed class EffectParams
        {
            public int DamagePermille;          // 비기 배율(T 의 배수, 2T = 2000). 부채·팔방·만개는 정면 줄기, 불가마는 지대 한 틱
            public int SideDamagePermille;      // 정면 밖 줄기의 배율(부채 700, 팔방 600, 만개 1200)
            public int BranchCount;             // 트리를 실행하는 줄기 수(마력 방출 1, 부채 3, 양익 2, 팔방·만개 8)
            public int BranchAngleCenti;        // 이웃 줄기 사이의 각(센티도. 부채 ±15도 = 1500, 양익 ±90도 = 9000, 팔방 45도 = 4500)
            public Fix64 Knockback;             // 적을 미는 거리(칸. 반동 사격 2)
            public Fix64 SelfMove;              // 시전자가 움직이는 거리(칸. 반동 사격 뒤로 2, 잔상 걸음 2.5, 벼락 질주 4)
            public int StaggerTicks;            // 경직(반동 사격의 돌진 취소 48, 벼락 질주 36, 그림자 손 30, 얼음 병풍에 부딪힌 돌진 90)
            public int StunTicks;               // 기절(뇌광창 60)
            public int BossHaltTicks;           // 보스가 기절 대신 받는 동작 멈춤(뇌광창 18)
            public int InvulnTicks;             // 시전자 무적(잔상 걸음 9, 벼락 질주 12)
            public Fix64 Width;                 // 폭(칸. 창 0.5, 벼락 질주의 길 1.0, 얼음 병풍 3)
            public Fix64 Thickness;             // 두께(칸. 얼음 병풍 0.5)
            public Fix64 Range;                 // 사거리(칸. 창 12)
            public Fix64 Radius;                // 반경(칸. 방열 폭발 3, 불가마 지대 2.5, 그림자 손 4.5)
            public Fix64 ClearRadius;           // 적 탄을 지우는 반경(칸. 방열 폭발·만개 3)
            public Fix64 Offset;                // 시야 정면으로 떨어진 거리(칸. 얼음 병풍 1.5, 그림자 손 3)
            public Fix64 ShardLength;           // 파편 범위의 길이(칸. 얼음 병풍 2. 폭은 Width)
            public Fix64 PullDistance;          // 끌어당기는 최대 거리(칸. 그림자 손 3)
            public int DurationTicks;           // 지속(불가마 240, 베껴 쓰기 360, 얼음 병풍 240)
            public int IntervalTicks;           // 간격(불가마 피해 틱 30, 베껴 쓰기의 따라 쏘기 최소 간격 18)
            public int RepeatIntervalPermilleTicks;   // 반복 간격(틱 × 1000. 만개 0.12초 = 7.2틱 = 7200. 반복 틱 = round(i × 7.2), spec 03 U-13)
            public int SlowPermille;            // 감속(천분율. 불가마 −20% = 200)
            public int HeatAbsorbHu;            // 대신 먹는 열의 합(불가마 16 = 1,920 HU)
            public int HeatPerTHu;              // 피해 T 하나에 드는 열(방열 폭발 "T × 열 ÷ 25" = 3,000 HU)
            public int MaxConcurrent;           // 동시에 있을 수 있는 수(불가마·베껴 쓰기·얼음 병풍 1)
            public int MaxCopies;               // 따라 쏘는 최대 횟수(베껴 쓰기 12)
            public int PrepTicks;               // 판정 뒤 효과까지의 준비(⑥ 5, ⑦ 4, ⑩ 15, spec 03 U-14 [임시])
            public int WallHp;                  // 벽 체력(결투의 얼음 병풍 150, 결투 체력 점수)
            public int TelegraphTicks;          // 상대에게 범위를 먼저 보여 주는 시간(결투의 그림자 손 18)
            public bool IgnoresArmor;           // 철갑 무시(꿰뚫는 창, 뇌광창)
        }

        public ArtId Id;
        public ArtKind Kind;
        public CharacterId Owner;                                   // 고유의 주인. 공용은 Baseline
        public CommandStep[][] Paths = Array.Empty<CommandStep[]>(); // spec 03 5.2. 루트는 길이 0 경로 하나(마력 방출)
        public bool IsJust;                                         // 섬 슬롯에 붙는다(뇌광창)
        public int Difficulty;                                      // 1, 2, 3, 5
        public ArtCostKind CostKind;
        public int Cost;                                            // Fixed 의 결정 수. AllCrystals 이면 0
        public int MinCost;                                         // AllCrystals 의 최소 결정 수. Fixed 이면 0
        public bool CreditAllowed;                                  // 그믐의 외상(만개만 거짓)
        public ArtId FallbackParent;                                // 고유 비기의 부모 대체 기술(경로의 부모 노드 K 기술). 공용은 None
        public UnlockRule Unlock = new UnlockRule();
        public EffectParams Effect = new EffectParams();            // 무한 웨이브·연습
        public EffectParams Duel = new EffectParams();              // 결투

        public bool IsUnique => Kind == ArtKind.Unique;

        public bool TakesAllCrystals => CostKind == ArtCostKind.AllCrystals;
    }
}
