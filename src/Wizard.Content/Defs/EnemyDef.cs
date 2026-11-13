using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 05 2.2(필드)·2.5(종별 수치)·4절(종별 공격). 원본 data/sim/enemies.json, 절 꼴은 Pack/Sections/ContentDb.Enemies.cs.
    // 판 안에서 바뀌는 값(체력 배율 H(w), 엘리트 ×3, 북두꺼비 버프)은 여기 없다. 이것은 종의 바탕 값이다(spec 05 2.5 "속도와 피해 칸 수는 곱하지 않는다").
    // spec 05 2.2의 그림 쪽 필드(canvas_px, body_px, palette), 문자열 필드(name_key = id, lore_key, archetype, affinity_notes)는
    // 재실행에 쓰이지 않으므로 이 절(코어 버전 해시 안)에 두지 않는다. 엘리트는 ContentDb.Elite(바탕 종)의 EliteDef 가 따로 가진다.
    // 이 타입이 쓰는 열거형(EnemyRole, TimeMask)과 AI 매개변수(AiParams)는 이 타입 안에 둔다(소유 파일 하나, 공개 최상위 타입 하나).
    public sealed class EnemyDef
    {
        public EnemyTypeId Id;
        public EnemyRole Role;
        public Fix64 BaseHp;                                // 체력. 판에서는 × H(w)(spec 05 3.7)
        public int MoveSpeedCenti;                          // 이동 속도, 센티 칸/초(3.0 → 300). 웨이브와 무관(spec 05 1.2 보충 5)
        public int AttackDamage;                            // spec 05 2.5 "피해" 열(칸). 예고 공격의 종별 값
        public int ContactDamage;                           // 몸 접촉 피해(칸). 0 이면 접촉이 없다(늑대, spec 05 3.6)
        public int Cost;                                    // 스폰 예산 비용. 처치 골드 = 비용 × 2(spec 05 2.5). 졸개는 0
        public int FirstWave;                               // 졸개는 0(웨이브 구성에 나오지 않는다)
        public TimeMask Times;                              // 나오는 시간대(spec 05 2.1 TimeMask). 졸개는 None(보스가 부른다)
        public Element Weak, Resist;                        // 약점 ×1.25, 저항 ×0.8(spec 05 1.3 A2). 둘은 다르고 None 이 아니다. 졸개는 둘 다 None(상성 없음, A4)
        public EnemyTag BaseTags;                           // 바탕 꼬리표. 상태에 따라 바뀌는 것(족제비의 땅속)은 상태 기계가 덮어쓴다
        public bool KnockbackImmune;                        // 꼬리표(중량·뿌리)의 표시용 사본(spec 05 2.2). 변환기가 꼬리표와 같은지 본다
        public int? SpeciesCap;                             // 같은 종 동시 상한(spec 05 2.5). null 이면 상한 없음
        public Fix64 BodyRadius;                            // 몸 판정 반지름(칸) [임시: spec 05 U-04]

        // 공격 목록. 순서가 첨자다(EnemyIntent.AttackIndex). 종마다 무엇이 몇 번인지는 data/sim/enemies.json 의 _note 에 적는다
        public AttackDef[] Attacks = Array.Empty<AttackDef>();

        public AiParams Ai = new AiParams();                // 종별 두뇌의 매개변수(spec 05 4절 (나)). 그 종이 쓰지 않는 값은 0

        // 보스의 졸개(갓버섯 새싹, spec 14 3.6 코드 101~). 꼬리표·상성·점수·골드·드랍이 없다(spec 05 6.1, 1.3 A4, 3.7 NoReward)
        public bool IsMinion;

        // 한 번 뽑힐 때 함께 나오는 수(spec 07 2.2 spawn_pack). 해거름 박쥐 6~8(spec 05 4.8), 나머지 1~1
        public int SpawnPackMin = 1, SpawnPackMax = 1;

        // spec 05 2.1 의 선언 그대로(값은 선언 순서, 0부터)
        public enum EnemyRole : byte
        {
            Swarm = 0,
            EvasiveCharger = 1,
            ArmoredHeavy = 2,
            RangedCover = 3,
            Suicide = 4,
            Sniper = 5,
            Ambush = 6,
            NightSwarm = 7,
            RingBarrage = 8,
            ZoneDenial = 9,
            LongCharger = 10,
            Support = 11,
        }

        // spec 05 2.1. 비트 = 1 << (TimeOfDay − 1). 보스 웨이브에는 일반 적이 나오지 않으므로 비트가 없다(spec 05 3.12)
        [Flags]
        public enum TimeMask : byte
        {
            None = 0,
            Day = 1,
            Dusk = 2,
            Night = 4,
            Always = 7,
        }

        // 종별 두뇌(M3-ENM-03 이후)가 읽는 수. 필드를 더하는 작업은 원본·스키마·변환기·절 읽기를 같이 고친다(절 항목 끝에 더한다)
        public sealed class AiParams
        {
            // 덤불 늑대 탄 회피(spec 05 4.2 wolf_bramble.dodge)
            public Fix64 DodgeDistance;                     // 대시 거리 1.5칸
            public int DodgeCooldownTicks;                  // 재사용 1.5초 = 90
            public int DodgeWindowTicks;                    // 이 안에 맞을 탄이면 회피(0.25초 = 15)
            public int DodgeDashTicks;                      // 대시 이동 시간 6틱 [임시: U-28]

            // 선돌지기 철갑(spec 05 4.3)
            public int ArmorHalfAngleCenti;                 // 정면 120도의 반각 60도 = 6000 센티도
            public int ArmorDamagePermille;                 // 임계가 아닌 피해 × 0.5 = 500(spec 05 3.7)

            // 갓버섯 술사 엄폐와 거리(spec 05 4.4)
            public int CoverHpPermille;                     // 체력이 최대의 이 비율 이하면 엄폐(절반 = 500)
            public Fix64 CoverOffset;                       // 엄폐물 + normalize(엄폐물 − 플레이어) × 1.5
            public Fix64 KeepDistanceMin, KeepDistanceMax;  // 평소 거리 5~8칸 [임시: U-36]
            public Fix64 KiteDistanceMin;                   // 엄폐물이 없을 때 7칸 이상 [임시: U-36]
            // 위 거리 유지 칸은 솔침 고슴도치(7~10칸, spec 05 4.6)와 도깨비불(5~7칸 [임시: U-53])도 쓴다

            // 꽈리 벌레 유폭(spec 05 4.5 bug_lantern.sympathetic). 반경은 점화 공격의 반경과 같다
            public Fix64 SympatheticDamage;                 // 적에게 주는 피해 20(× H(w), 무속성). 플레이어에게는 0
            public int SympatheticDelayTicks;               // 0.4초 = 24

            // 화분 족제비 땅속·지상(spec 05 4.7)
            public int SurfaceTicks;                        // 솟은 뒤 지상 1.6초 = 96(빈틈이 아니다, U-51)
            public int DigTicks;                            // 다시 파고들기 0.5초 = 30
            public int BurrowMaxTicks;                      // 한 번에 땅속 최대 4초 = 240
            public int BurrowRetargetTicks;                 // 요청 뒤 3초(180) 더 막히면 목표를 자기 위치로 [임시: U-50]

            // 해거름 박쥐 물결(spec 05 4.8). 실제 이동량은 이동 속도로 자른다 [임시: U-52]
            public Fix64 WaveAmplitude;                     // 진폭 1칸
            public int WavePeriodTicks;                     // 주기 1.2초 = 72

            // 도깨비불 깜박 이동(spec 05 4.9 wisp_stray.blink)
            public int BlinkPeriodTicks;                    // 5초마다 = 300
            public int VanishTicks, AppearTicks;            // 사라지기·나타나기 0.3초 = 18
            public int NoCastAfterAppearTicks;              // 나타난 뒤 0.5초 = 30 시전 금지
            public Fix64 BlinkDistanceMin, BlinkDistanceMax;    // 플레이어에게서 5~7칸
            public Fix64 BlinkRetryDistance;                // 후보가 없으면 반지름 6 고정으로 다시 [임시: U-56]
            public int BlinkCandidates;                     // 각도 후보 16개 [임시: U-56]
            public Fix64 BlinkWallMargin;                   // 벽에서 1칸 이상 [임시: U-56]

            // 가시덩굴 등장(spec 05 4.10)
            public int EmergeTicks;                         // 나오기 전 흙 금 1.0초 = 60(피해 주고받지 않음)
            public Fix64 SpawnWallMargin;                   // 벽에서 2칸 이상
            public Fix64 SpawnPlayerMargin;                 // 플레이어와 3칸 이상 [임시: U-60]

            // 엄니 멧돼지(spec 05 4.11). 벽 충돌 기절 2.0초는 돌진 공격의 RecoveryTicks 다
            public int StunDamagePermille;                  // 기절 중 받는 피해 × 1.5 = 1500
            public Fix64 PathDamage;                        // 돌진 경로의 다른 적에게 10(× H(w), 무속성)

            // 북두꺼비 울음과 따라가기(spec 05 4.12, 3.8)
            public int CroakPeriodTicks;                    // 6초마다 = 360(울음 시작 기준, U-65)
            public int CroakTicks;                          // 우는 시간 2.0초 = 120
            public Fix64 BuffRadius;                        // 반경 4 안의 다른 적
            public int BuffTicks;                           // 4초 = 240
            public int BuffSpeedPermille;                   // 이동 속도 +20% = 1200
            public int BuffSpeedCapCenti;                   // 버프 뒤 최종 속도 상한 3.7 = 370
            public int BuffRecoveryPermille;                // 예고 뒤 빈틈 −30% = 700(올림, U-19)
            public Fix64 CroakBreakDamage;                  // 울음 끊기 누적 피해 30(× H(w))
            public int CroakStaggerTicks;                   // 끊기면 1.0초 = 60 경직
            public Fix64 FollowRadius;                      // 무리 중심을 셀 다른 적의 반경 8 [임시: U-64]
            public Fix64 FollowOffset;                      // 무리 중심에서 플레이어 반대쪽 2칸 [임시: U-64]
        }
    }
}
