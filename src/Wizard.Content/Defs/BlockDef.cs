using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2, spec 02 4.2. 원본 data/sim/blocks.json, 절 꼴은 Pack/Sections/ContentDb.Blocks.cs
    public sealed class BlockDef
    {
        public BlockId Id;
        public BlockKind Kind;
        public Rarity Rarity;
        public Element Element;
        public int HeatHu;                                  // 투사체·변형이 더하는 열
        public int HeatMulPermille;                         // 분열만 1600 / 2200. 그 밖 1000
        public int SplitCount;                              // 분열만 2 / 3
        public Fix64 BaseDamage;
        public int IntervalTicks;
        public Fix64 SpeedPerTick;
        public bool IsInstant;
        public Fix64 Range;
        public Fix64 HitRadius;
        public int DamageBonusPermille;                     // 변형: 200, 100
        public ProjectileFlags Flags;
        public ModifierEffect Effect;
        public BlockParams Params = new BlockParams();      // 폭발 반경, 전이 횟수, 감속 %, 지속 틱 등
    }
}
