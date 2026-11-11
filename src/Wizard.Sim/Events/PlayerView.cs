using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3, spec 01 17.1의 표
    public readonly struct PlayerView
    {
        public readonly EntityId Id;
        public readonly CharacterId Character;
        public readonly FixVec2 Pos;
        public readonly Dir8 Facing;
        public readonly StanceMode Stance;
        public readonly int HpPoints, MaxHpPoints;
        public readonly bool Invulnerable;
        public readonly int HeatHu, CritStartHu;
        public readonly HeatRingState HeatRing;
        public readonly bool ExemptArmed;
        public readonly int LockRemainTicks, LockTotalTicks;
        public readonly int Combo, ComboIdleTicks;
        public readonly bool ComboTimerPaused;
        public readonly int Crystals, CrystalCap, Debt, Crack;
        public readonly bool GrazeBuffActive;
        public readonly ArtPreview Preview;               // "지금 K = …" (spec 03 11절)
        public readonly RecentInputs Recent;              // 최근 입력 5개
        public readonly KeyMask Keys;                     // 지금의 논리 키 상태(결투의 키 상태 맞추기, CONTRACTS 6.4)
    }
}
