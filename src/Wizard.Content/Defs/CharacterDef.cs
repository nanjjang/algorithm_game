using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 01 5.1, spec 04 2.2. 팩에서 읽은 뒤 바뀌지 않는다
    public sealed class CharacterDef
    {
        public CharacterId Id;
        public Element Element;
        public int MaxHpCells;                              // 칸. 체력 점수 = 칸 × 100
        public int WalkSpeedCenti, StanceSpeedCenti;        // 센티 칸/초 (4.7 → 470)
        public Fix64 HitRadius, BodyRadius, GrazeRadius;
        public int InvulnTicks;
        public int CrystalCap, ComboPerCrystal, CrystalLossOnHit;
        public int BaseLinkSlots, MaxMutations;
        public int CoolHuPerTick, CoolDelayTicks, CritStartHu, LockTicks;
        public int GrazeBuffTicks, GrazeBuffPermille;
        public PassiveId Passive;
        public WandTraitId WandTrait;
        public ArtId UniqueArt;
        public DuelCharacterOverride Duel = new DuelCharacterOverride();   // spec 04 8절, spec 10 7절
    }
}
