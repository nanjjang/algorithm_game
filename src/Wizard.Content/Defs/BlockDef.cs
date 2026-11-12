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
        public int SplitCount;                              // 분열만 2 / 3. 그 밖 1
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

        // 제어 블록의 갈래(spec 02 4.2 의 ControlType. T-53: 필드를 따로 두지 않고 Kind 와 SplitCount 로 가른다).
        // 분열은 갈래가 둘 이상인 제어 블록, 적중 시 발동은 갈래가 하나인 제어 블록이다. 변환기가 이 둘 말고는 받지 않는다
        public bool IsSplit => Kind == BlockKind.Control && SplitCount > 1;
        public bool IsTrigger => Kind == BlockKind.Control && SplitCount == 1;
    }
}
