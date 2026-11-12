using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 적 탄 하나를 표시 계층이 읽는 값(읽기 전용). ProtoReadout 이 준다
    public readonly struct ProtoEnemyShotView
    {
        public readonly EntityId Id;
        public readonly int CastId;
        public readonly FixVec2 Pos;
        public readonly FixVec2 Dir;
        public readonly Fix64 Radius;

        public ProtoEnemyShotView(EntityId id, int castId, FixVec2 pos, FixVec2 dir, Fix64 radius)
        {
            Id = id;
            CastId = castId;
            Pos = pos;
            Dir = dir;
            Radius = radius;
        }
    }
}
