using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 고정 조립(spec 01 13절 6번: 고정 투사체 2종과 3갈래 전환). 코어 = 마력탄 또는 화염구, 첫 연결 칸 = 3갈래 분열 또는 비움.
    // 실제 주문 컴파일러(M2-SPL-02)의 SpellTable 자리를 시제품이 이것으로 쓴다. 값 형식이다
    internal struct ProtoLoadout
    {
        public BlockId Core;
        public bool SplitThree;

        // 바이트: core u8, split bool
        public void Write(ref StateWriter w)
        {
            w.U8((byte)Core);
            w.Bool(SplitThree);
        }

        public void Read(ref StateReader r)
        {
            Core = (BlockId)r.U8();
            SplitThree = r.Bool();
            if (!IsCoreBlock(Core))
            {
                throw new SimInvariantException("proto: loadout core is not a prototype projectile");
            }
        }

        public static bool IsCoreBlock(BlockId id) => id == BlockId.MagicBolt || id == BlockId.Fireball;
    }
}
