using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3, spec 01 17.1의 표. 세계가 매 틱 플레이어 상태에서 만들어 준다(읽기 전용 값)
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

        public PlayerView(
            EntityId id, CharacterId character, FixVec2 pos, Dir8 facing, StanceMode stance,
            int hpPoints, int maxHpPoints, bool invulnerable,
            int heatHu, int critStartHu, HeatRingState heatRing, bool exemptArmed,
            int lockRemainTicks, int lockTotalTicks,
            int combo, int comboIdleTicks, bool comboTimerPaused,
            int crystals, int crystalCap, int debt, int crack,
            bool grazeBuffActive, ArtPreview preview, RecentInputs recent, KeyMask keys)
        {
            Id = id;
            Character = character;
            Pos = pos;
            Facing = facing;
            Stance = stance;
            HpPoints = hpPoints;
            MaxHpPoints = maxHpPoints;
            Invulnerable = invulnerable;
            HeatHu = heatHu;
            CritStartHu = critStartHu;
            HeatRing = heatRing;
            ExemptArmed = exemptArmed;
            LockRemainTicks = lockRemainTicks;
            LockTotalTicks = lockTotalTicks;
            Combo = combo;
            ComboIdleTicks = comboIdleTicks;
            ComboTimerPaused = comboTimerPaused;
            Crystals = crystals;
            CrystalCap = crystalCap;
            Debt = debt;
            Crack = crack;
            GrazeBuffActive = grazeBuffActive;
            Preview = preview;
            Recent = recent;
            Keys = keys;
        }
    }
}
