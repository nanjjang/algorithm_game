using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 플레이어 탄 하나를 표시 계층이 읽는 값(읽기 전용). ProtoReadout 이 준다
    public readonly struct ProtoBulletView
    {
        public readonly EntityId Id;
        public readonly PlayerSlot Owner;
        public readonly BlockId Block;
        public readonly FixVec2 Pos;
        public readonly FixVec2 Dir;
        public readonly Fix64 Radius;

        public ProtoBulletView(EntityId id, PlayerSlot owner, BlockId block, FixVec2 pos, FixVec2 dir, Fix64 radius)
        {
            Id = id;
            Owner = owner;
            Block = block;
            Pos = pos;
            Dir = dir;
            Radius = radius;
        }
    }
}
