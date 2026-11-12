using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 포탑의 3갈래 탄 하나(spec 05 4.4 shaman_cap.triple). 속도·반지름·피해는 ProtoTuning 의 값이라 담지 않는다.
    // 값 형식이고 참조가 없다. 풀은 id 오름차순이다
    internal struct ProtoEnemyShot
    {
        public EntityId Id;
        public int CastId;                 // 시전 묶음(spec 01 8.2). 한 번의 발사(3발)가 한 묶음이다. 1 이상
        public FixVec2 Pos;
        public FixVec2 Dir;                // 단위 벡터
        public Fix64 RangeLeft;
        public bool Dead;                  // 틱 안의 표시(9단계 끝에 풀에서 뺀다)

        // 바이트: id i32, cast i32, pos vec, dir vec, range fix
        public void Write(ref StateWriter w)
        {
            w.I32(Id.Value);
            w.I32(CastId);
            w.Vec(Pos);
            w.Vec(Dir);
            w.Fix(RangeLeft);
        }

        public void Read(ref StateReader r)
        {
            Id = new EntityId(r.I32());
            CastId = r.I32();
            Pos = r.Vec();
            Dir = r.Vec();
            RangeLeft = r.Fix();
            Dead = false;
            if (Id.IsNone || CastId < 1)
            {
                throw new SimInvariantException("proto: malformed enemy shot in state");
            }
        }
    }
}
