using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. 모드 스위치(spec 01 12.1의 표). 모드 차이는 이 값이다(ARCHITECTURE P1).
    // 공장 함수는 모드마다 정해진 켬·끔 스위치(spec 01 12.1, CONTRACTS 3.2의 AllowDebugOps)만 여기서 정하고,
    // 수치 칸(투사체 상한, 창 틱, 천분율, 판정 반지름)은 데이터에서 읽어 채우는 부분 메서드(Fill*)에 맡긴다.
    // 기획 수치는 data/ 에만 둔다(CLAUDE.md 5절). Fill* 이 아직 없는 동안 수치 칸은 0이다(OPEN-QUESTIONS).
    public sealed partial class SimConfig
    {
        public SimMode Mode;
        public bool HitRadiusOverrideOn;
        public Fix64 HitRadiusOverride;
        public int InvulnTicksOverride = -1;              // −1이면 캐릭터 값
        public int BurstCapPermille, BurstWindowTicks;
        public int GrazeCooldownTicks;
        public bool GrazeFullReward;
        public int PenaltyWindowTicks, SkillPrepTicks, StatusApplyDelayTicks;
        public int AimAssistCenti;
        public int PlayerProjectileCap, EnemyProjectileCap;
        public bool ProjectileCapPerPlayer;
        public bool FireEnabled, SkillEnabled, StarfallEnabled, UnstableCoreSelfBlowup, AllArtsUnlocked;
        public bool AllowDebugOps;                        // RunCommandOp 200~255를 받을지. ForEndless·ForDuel·ForPlaza 는 false. 오프라인 개발 빌드의 디버그 메뉴와 연습만 켠다

        // 무한 웨이브. 피격 무적은 캐릭터 값, 스침은 전부 보상, 별똥·불안정 핵 자폭 켬(spec 01 12.1)
        public static SimConfig ForEndless(ContentDb db)
        {
            RequireDb(db);
            var c = new SimConfig
            {
                Mode = SimMode.Endless,
                HitRadiusOverrideOn = false,
                InvulnTicksOverride = -1,
                GrazeFullReward = true,
                ProjectileCapPerPlayer = false,
                FireEnabled = true,
                SkillEnabled = true,
                StarfallEnabled = true,
                UnstableCoreSelfBlowup = true,
                AllArtsUnlocked = false,
                AllowDebugOps = false,
            };
            FillEndless(db, c);
            return c;
        }

        // 결투. 판정 반지름·무적·순간 피해 상한·스침 간격·벌칙 창·비기 준비·상태 지연·조준 보정은 데이터(duel_rules)에서 채운다.
        // 스침은 줄어든 보상, 투사체 상한은 한 사람당, 별똥·날씨·불안정 핵 자폭 없음(spec 01 12.1, online 4.2)
        public static SimConfig ForDuel(ContentDb db, DuelRoomOptions options)
        {
            RequireDb(db);
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            var c = new SimConfig
            {
                Mode = SimMode.Duel,
                HitRadiusOverrideOn = true,
                GrazeFullReward = false,
                ProjectileCapPerPlayer = true,
                FireEnabled = true,
                SkillEnabled = true,
                StarfallEnabled = false,
                UnstableCoreSelfBlowup = false,
                AllArtsUnlocked = false,
                AllowDebugOps = false,
            };
            FillDuel(db, options, c);
            return c;
        }

        // 광장. 같은 조작으로 걷고 발사와 비기는 꺼진다(spec 01 4.1, wc 2.5)
        public static SimConfig ForPlaza(ContentDb db)
        {
            RequireDb(db);
            var c = new SimConfig
            {
                Mode = SimMode.Plaza,
                HitRadiusOverrideOn = false,
                InvulnTicksOverride = -1,
                GrazeFullReward = false,
                ProjectileCapPerPlayer = false,
                FireEnabled = false,
                SkillEnabled = false,
                StarfallEnabled = false,
                UnstableCoreSelfBlowup = false,
                AllArtsUnlocked = false,
                AllowDebugOps = false,
            };
            FillPlaza(db, c);
            return c;
        }

        // 연습(허수아비·봇). 바탕 설정을 값으로 복사하고 모드만 바꾼다. 디버그 op 은 연습에서 켠다(CONTRACTS 3.2)
        public static SimConfig ForPractice(ContentDb db, SimConfig basedOn)
        {
            RequireDb(db);
            if (basedOn == null)
            {
                throw new ArgumentNullException(nameof(basedOn));
            }
            SimConfig c = basedOn.Copy();
            c.Mode = SimMode.Practice;
            c.AllowDebugOps = true;
            FillPractice(db, c);
            return c;
        }

        private SimConfig Copy() => (SimConfig)MemberwiseClone();   // 참조 칸이 없으므로 얕은 복사가 깊은 복사다

        // 데이터 절의 수치를 채우는 자리. 그 절을 읽는 작업이 같은 이름의 부분 클래스 파일에서 구현한다
        static partial void FillEndless(ContentDb db, SimConfig config);

        static partial void FillDuel(ContentDb db, DuelRoomOptions options, SimConfig config);

        static partial void FillPlaza(ContentDb db, SimConfig config);

        static partial void FillPractice(ContentDb db, SimConfig config);

        private static void RequireDb(ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
        }
    }
}
