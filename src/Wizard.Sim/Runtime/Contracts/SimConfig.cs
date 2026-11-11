using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. 모드 스위치(spec 01 12.1의 표). 모드 차이는 이 값이다(ARCHITECTURE P1).
    // ForEndless/ForDuel/ForPlaza/ForPractice 공장 함수는 데이터에서 값을 읽으므로 M1-SIM-02 가 더한다
    public sealed class SimConfig
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
        public bool AllowDebugOps;                        // RunCommandOp 200~255를 받을지. ForEndless·ForDuel·ForPlaza 는 false
    }
}
