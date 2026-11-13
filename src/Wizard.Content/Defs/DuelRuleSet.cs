using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 10 2절·17.2절. 각도는 센티도, 반지름은 Fix64. 전투 규칙만(코어 버전 해시 안).
    // 원본 data/sim/duel_rules.json, 절 꼴은 Pack/Sections/ContentDb.DuelRules.cs. 팩에서 읽은 뒤 바뀌지 않는다.
    // 담는 것: spec 10 2.1(경기 형식) 2.2(결투에서 바뀌는 전투 수치) 2.3(스침과 벌칙) 2.4(조준), 3.4(가시밭)·3.5(야생 난입)의 [임시] 값,
    // 9.2(방 옵션)의 선택지와 기본값, 7.2~7.3(캐릭터별 결투 값).
    // 여기 없는 것: 2.5의 매치·입장, 2.6 IL·리그, 2.7 점수·보상, 2.8 넷코드는 재실행에 영향이 없어 규칙 쪽 절(해시 밖)이 가진다.
    // 공용 비기의 결투 값(반동 사격 넉백 1.5칸, 뇌광창 기절 30틱)과 고유 비기의 결투 값은 ArtDef.Duel(spec 03 16절)이 가진다.
    // 켬·끔 스위치(별똥 없음, 불안정 핵 자폭 없음, 스침 보상 축소)는 SimConfig.ForDuel 이 정한다(spec 01 12.1).
    public sealed class DuelRuleSet
    {
        // spec 10 9.2 방 옵션의 선택지와 기본값. DuelRoomOptions(CONTRACTS 3.9)는 수를 갖지 않고 기본 옵션을 이 값으로 만든다(T-48).
        // 값이 선택지 안인지는 방을 만드는 서버가 이 표로 본다
        public sealed class RoomRules
        {
            public const int NoCap = -1;                    // 상한 "없음". DuelRoomOptions.NoCap 과 같은 값

            public int[] RoundsChoices = Array.Empty<int>();            // 1 / 3 / 5 (선승 1 / 2 / 3). 오름차순, 홀수
            public int DefaultRounds;                                   // 3
            public int[] RoundSecondsChoices = Array.Empty<int>();      // 60 / 90 / 120 (초, DuelRoomOptions.RoundSeconds 의 단위)
            public int DefaultRoundSeconds;                             // 90
            public int[] HpMultPermilleChoices = Array.Empty<int>();    // ×0.7 / ×1 / ×1.5 = 700 / 1000 / 1500
            public int DefaultHpMultPermille;                           // 1000
            public int EnhanceCapMin, EnhanceCapMax, DefaultEnhanceCap; // 0, 13, 13
            public int IlCapMin, IlCapMax, DefaultIlCap;                // 0, 237, NoCap
            public bool DefaultAllowMutations;                          // 참
            public int HeroBlockCapMin, HeroBlockCapMax, DefaultHeroBlockCap;   // 0, 8, NoCap
            public bool DefaultEqualize, DefaultWildIntrusion, DefaultThornsOff;   // 거짓
            public TimeOfDay[] FloorColorChoices = Array.Empty<TimeOfDay>();    // Day / Dusk / Night (색만 바꾼다)
            public TimeOfDay DefaultFloorColor;                         // Day
            public bool DefaultSpectate;                                // 거짓
            public int SpectatorMax;                                    // 6 (2.5 room_spectator_max)
        }

        // ── 2.1 경기 형식 ──
        public int Players;                                 // 2
        public int RoundsToWin;                             // 2 (자동 매치·광장 도전의 3판 2선승)
        public int RoundLengthTicks;                        // 90초 = 5,400
        public int CountdownTicks;                          // 3초 = 180
        public int IntermissionTicks;                       // 라운드 사이 3초 = 180 [임시 U-26]
        public int HpPerCell;                               // 100. 결투 체력 = HpPerCell × CharacterDef.MaxHpCells
        public int RoundStartHeatHu;                        // 0
        public int RoundStartCrystals;                      // 1
        public int OvertimeTicks;                           // 30초 = 1,800
        public int OvertimeDamagePermille;                  // 받는 피해 ×2 = 2,000

        // 가시밭(2.1, 3.4). 시작 = 라운드 길이 − ThornLeadTicks(U-02. 90초 라운드에서 ThornStartTicks)
        public int ThornStartTicks;                         // 60초 = 3,600
        public int ThornLeadTicks;                          // 30초 = 1,800 [임시]
        public int ThornStepTicks;                          // 5초 = 300
        public int ThornDamageIntervalTicks;                // 0.5초 = 30
        public int ThornDamagePermille;                     // 최대 체력의 4% = 40 (올림, 3.4)
        public int ThornRingMax;                            // 6 [임시 U-03]

        // 야생 난입(2.1, 3.5). 첫 출현은 라운드 경과 WildSpawnIntervalTicks
        public EnemyTypeId WildEnemy;                       // enemy.slime_moss
        public int WildSpawnIntervalTicks;                  // 20초 = 1,200
        public int WildSpawnCount;                          // 4
        public int WildContactDamage;                       // 100 (체력)
        public int WildContactCooldownTicks;                // 한 마리가 같은 플레이어에게 60틱에 1번 [임시 U-27]
        public FixVec2[] WildSpawnPoints = Array.Empty<FixVec2>();     // 네 구석 (1.5,1.5) (22.5,1.5) (1.5,12.5) (22.5,12.5) [임시 U-27]

        // ── 2.2 결투에서 바뀌는 전투 수치 ──
        public int EnhancePerStagePermille;                 // 강화 배율 1 + 0.03 × 단계 → 30
        public int ElementMultPermille;                     // 속성 배율 없음 = 1,000
        public int BurstWindowTicks;                        // 0.5초 = 30
        public int BurstCapPermille;                        // 최대 체력의 30% = 300
        public int PostHitInvulnTicks;                      // 0 (피격 뒤 무적 없음)
        public Fix64 HitRadius;                             // 0.30칸 = 1,288,490,189
        public Fix64 HitCenterOffsetY;                      // 발밑에서 위로 0.4칸 = 1,717,986,918 (y 는 −)
        public int ChainLightningAimTicks;                  // 0.25초 = 15
        public int ChainLightningJumps;                     // 0 (전이 없음)
        public int HomingTurnPermille;                      // 선회각 절반 = 500
        public int HomingDurationTicks;                     // 1.5초 = 90
        public int IceSpikeSlowPermille, IceSpikeSlowTicks; // −15% = 150, 1초 = 60
        public int FrostSlowPermille, FrostSlowTicks;       // −10% = 100, 1초 = 60
        public int ErosionBonusPermille, ErosionTicks;      // +8% = 80, 3초 = 180
        public int StatusApplyDelayTicks;                   // 적중 3틱 뒤
        public int ArtPrepTicks;                            // 판정 뒤 준비 2틱
        public int BulletCapPerPlayer;                      // 한 사람당 150 (사본·분열 포함)
        public int UnstableCoreRadiusPermille;              // 불안정 핵 폭발 반경 ×1.5 = 1,500 (자폭 없음)
        public int GlassWandOffTicks;                       // 유리 지팡이: 맞은 뒤 3초 = 180 임계 +1 꺼짐

        // ── 2.3 스침과 벌칙 ──
        public int GrazeAcceptTicks;                        // 상대 한 명당 1.0초 = 60에 1회
        public int GrazeCombo;                              // +1
        public int PenaltyIntervalTicks;                    // 1.0초 = 60에 1회
        public int PenaltyWindowTicks;                      // 1.0초 = 60

        // ── 2.4 조준 ──
        public int AimAssistMaxCenti;                       // 10도 = 1,000
        public int AimAssistFallbackCenti;                  // 15도 = 1,500
        public Fix64 AimLineLength;                         // 0.5칸 (내 화면에만)
        public int TwistedBarrelJitterCenti;                // ±25도 = 2,500 (보정 뒤에 더한다)

        // ── 9.2 방 옵션 ──
        public RoomRules Room = new RoomRules();

        // ── 7.2~7.3 캐릭터별 결투 값. CharacterId 값이 첨자(Baseline 칸은 null). CharacterDef.Duel 과 같은 객체 ──
        public DuelCharacterOverride?[] Characters = Array.Empty<DuelCharacterOverride?>();
    }
}
