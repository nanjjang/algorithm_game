using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 플레이어 탄 하나(고정 투사체 2종: 마력탄, 화염구). 값 형식이고 참조가 없다. 풀은 id 오름차순이다(만든 순서).
    internal struct ProtoBullet
    {
        public EntityId Id;
        public PlayerSlot Owner;
        public int ShotId;                 // J 발사 1회의 묶음 id(HeatFire.ShotId). 비기의 탄은 0
        public BlockId Block;              // 코어 블록
        public Element Element;
        public FixVec2 Pos;
        public FixVec2 Dir;                // 단위 벡터
        public Fix64 SpeedPerTick;
        public Fix64 RangeLeft;            // 남은 사거리(칸). 0 이하가 되면 사라진다
        public Fix64 Radius;
        public Fix64 Damage;               // 임계·비기 배율까지 곱한 값(속성 배율은 맞을 때)
        public bool Knockback;             // 반동 사격의 탄: 맞은 적을 탄 방향으로 민다
        public bool Dead;                  // 틱 안의 표시(9단계 끝에 풀에서 뺀다). 틱 경계에서는 언제나 거짓

        // 바이트: id i32, owner u8, shot i32, block u8, element u8, pos vec, dir vec, speed fix, range fix, radius fix,
        //         damage fix, knockback bool
        public void Write(ref StateWriter w)
        {
            w.I32(Id.Value);
            w.U8(Owner.Value);
            w.I32(ShotId);
            w.U8((byte)Block);
            w.U8((byte)Element);
            w.Vec(Pos);
            w.Vec(Dir);
            w.Fix(SpeedPerTick);
            w.Fix(RangeLeft);
            w.Fix(Radius);
            w.Fix(Damage);
            w.Bool(Knockback);
        }

        public void Read(ref StateReader r)
        {
            Id = new EntityId(r.I32());
            Owner = new PlayerSlot(r.U8());
            ShotId = r.I32();
            Block = (BlockId)r.U8();
            Element = (Element)r.U8();
            Pos = r.Vec();
            Dir = r.Vec();
            SpeedPerTick = r.Fix();
            RangeLeft = r.Fix();
            Radius = r.Fix();
            Damage = r.Fix();
            Knockback = r.Bool();
            Dead = false;
            if (Id.IsNone || ShotId < 0 || Element > Element.Dark || Radius < Fix64.Zero)
            {
                throw new SimInvariantException("proto: malformed bullet in state");
            }
        }
    }
}
