using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 01 5.1, spec 04 2.2. 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/characters.json(baseline = CharacterId.Baseline, characters = char.1~char.5), 절 꼴은 Pack/Sections/ContentDb.Characters.cs.
    // 깊이 감쇠(0.85 / 0.92)는 캐릭터가 아니라 지팡이 쪽 값이다(spec 02 5.2, spec 04 7.1)
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
