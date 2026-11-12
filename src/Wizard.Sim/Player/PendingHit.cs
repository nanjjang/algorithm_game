using Wizard.Core;

namespace Wizard.Sim.Player
{
    // spec 01 6.2 "같은 틱에 두 공격이 닿으면 id 가 작은 공격 하나만 들어가고 나머지는 무적에 막힌다" [임시].
    // 9단계(충돌)가 이번 틱의 피격 후보를 넣고, 10단계(피해 확정, Stage10_Player)가 하나만 확정하고 비운다.
    // 그래서 틱 경계에서는 언제나 비어 있다. 그래도 세계 상태와 같이 직렬화한다(Step 밖에서 넣은 후보도 되감기·해시가 어긋나지 않게).
    internal struct PendingHit
    {
        public bool Active;
        public EntityId Attack;            // 공격 id(탄·돌진·접촉·예고 구역의 개체 id). 작은 쪽이 이긴다
        public int DamageCells;            // 이미 적 쪽 3칸 상한이 걸린 칸 수(mon 4.2). 1 이상

        public void Write(ref StateWriter w)
        {
            w.Bool(Active);
            w.I32(Attack.Value);
            w.I32(DamageCells);
        }

        public void Read(ref StateReader r)
        {
            Active = r.Bool();
            Attack = new EntityId(r.I32());
            DamageCells = r.I32();
            if (Active && DamageCells < 1)
            {
                throw new SimInvariantException("player: pending hit without damage");
            }
        }
    }
}
