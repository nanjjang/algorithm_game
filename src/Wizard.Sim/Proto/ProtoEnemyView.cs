using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 적 하나(슬라임, 포탑)를 표시 계층이 읽는 값(읽기 전용). ProtoReadout 이 준다.
    // 포탑이 시전 중이면(Casting) 시전이 끝나는 틱(CastEndTick)에 3발이 나간다. 예고의 채움은 이 값으로만 센다
    public readonly struct ProtoEnemyView
    {
        public readonly EntityId Id;
        public readonly ProtoEnemyKind Kind;
        public readonly FixVec2 Pos;
        public readonly Fix64 BodyRadius;
        public readonly Fix64 Hp, MaxHp;
        public readonly bool Casting;
        public readonly uint CastEndTick;

        public ProtoEnemyView(EntityId id, ProtoEnemyKind kind, FixVec2 pos, Fix64 bodyRadius, Fix64 hp, Fix64 maxHp,
                              bool casting, uint castEndTick)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            BodyRadius = bodyRadius;
            Hp = hp;
            MaxHp = maxHp;
            Casting = casting;
            CastEndTick = castEndTick;
        }
    }
}
