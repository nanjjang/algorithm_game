using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // PlayerView 를 모듈들이 나눠 채우는 임시 값(CONTRACTS 3.3 "뷰 훅", T-54). 칸은 PlayerView 와 같고 그 칸의 상태를 가진 모듈만 쓴다:
    //   Player — Id, Character, Pos, Facing, Stance, HpPoints, MaxHpPoints, Invulnerable, Keys
    //   Heat   — HeatHu, CritStartHu, HeatRing, ExemptArmed, LockRemainTicks, LockTotalTicks
    //   Graze  — GrazeBuffActive
    //   Combo  — Combo, ComboIdleTicks, ComboTimerPaused, Crystals, CrystalCap, Debt, Crack
    //   Arts   — Preview, Recent
    // 채우지 않은 칸은 기본값이다(그 모듈이 없거나 시작 상태가 아직 서지 않았다).
    internal struct PlayerViewDraft
    {
        public EntityId Id;
        public CharacterId Character;
        public FixVec2 Pos;
        public Dir8 Facing;
        public StanceMode Stance;
        public int HpPoints, MaxHpPoints;
        public bool Invulnerable;
        public int HeatHu, CritStartHu;
        public HeatRingState HeatRing;
        public bool ExemptArmed;
        public int LockRemainTicks, LockTotalTicks;
        public int Combo, ComboIdleTicks;
        public bool ComboTimerPaused;
        public int Crystals, CrystalCap, Debt, Crack;
        public bool GrazeBuffActive;
        public ArtPreview Preview;
        public RecentInputs Recent;
        public KeyMask Keys;

        public PlayerView ToView() =>
            new PlayerView(Id, Character, Pos, Facing, Stance, HpPoints, MaxHpPoints, Invulnerable,
                           HeatHu, CritStartHu, HeatRing, ExemptArmed, LockRemainTicks, LockTotalTicks,
                           Combo, ComboIdleTicks, ComboTimerPaused, Crystals, CrystalCap, Debt, Crack,
                           GrazeBuffActive, Preview, Recent, Keys);
    }
}
