using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 적 하나(슬라임 또는 포탑). 값 형식이고 참조가 없다. 풀은 id 오름차순이다
    internal struct ProtoEnemy
    {
        public EntityId Id;
        public ProtoEnemyKind Kind;
        public FixVec2 Pos;
        public Fix64 Hp, MaxHp;
        public uint ContactReadyTick;      // 슬라임: tick ≥ 이 값이면 접촉 피해를 줄 수 있다(spec 05 3.6)
        public FixVec2 MoveDir;            // 7단계의 이동 의도(단위 벡터 또는 0). 9단계가 움직인다
        public ProtoTurretPhase Phase;     // 포탑
        public uint PhaseEndTick;          // 포탑: Cast·Recovery 가 끝나는 틱
        public uint NextCastTick;          // 포탑: 다음 시전을 시작할 수 있는 틱
        public Fix64 PendingDamage;        // 9단계(탄)·5단계(창)가 쌓고 10단계가 확정한다. 틱 경계에서는 0
        public FixVec2 PendingKnock;       // 반동 사격의 넉백. 10단계에 옮긴다. 틱 경계에서는 0

        // 바이트: id i32, kind u8, pos vec, hp fix, max_hp fix, contact_ready u32, move_dir vec,
        //         phase u8, phase_end u32, next_cast u32, pending_damage fix, pending_knock vec
        public void Write(ref StateWriter w)
        {
            w.I32(Id.Value);
            w.U8((byte)Kind);
            w.Vec(Pos);
            w.Fix(Hp);
            w.Fix(MaxHp);
            w.U32(ContactReadyTick);
            w.Vec(MoveDir);
            w.U8((byte)Phase);
            w.U32(PhaseEndTick);
            w.U32(NextCastTick);
            w.Fix(PendingDamage);
            w.Vec(PendingKnock);
        }

        public void Read(ref StateReader r)
        {
            Id = new EntityId(r.I32());
            Kind = (ProtoEnemyKind)r.U8();
            Pos = r.Vec();
            Hp = r.Fix();
            MaxHp = r.Fix();
            ContactReadyTick = r.U32();
            MoveDir = r.Vec();
            Phase = (ProtoTurretPhase)r.U8();
            PhaseEndTick = r.U32();
            NextCastTick = r.U32();
            PendingDamage = r.Fix();
            PendingKnock = r.Vec();
            if (Id.IsNone || Kind == ProtoEnemyKind.None || Kind > ProtoEnemyKind.Turret || Phase > ProtoTurretPhase.Recovery
                || Hp <= Fix64.Zero || Hp > MaxHp || PendingDamage < Fix64.Zero)
            {
                throw new SimInvariantException("proto: malformed enemy in state");
            }
        }
    }
}
