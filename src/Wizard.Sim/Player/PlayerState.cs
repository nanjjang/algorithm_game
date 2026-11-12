using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim.Player
{
    // spec 01 3.9 의 PlayerState 중 Player 모듈(절 1)의 필드: 위치, 시야, 자세, 키 상태, 체력, 무적.
    // 과열·스침·콤보·결정·결투의 필드는 그 모듈의 절에 있다(CONTRACTS 3.2 훅 표).
    // 값 형식이고 참조가 없다. 스냅숏은 값 복사, 직렬화는 아래 Write/Read 의 순서 그대로다.
    internal struct PlayerState
    {
        public CharacterId Character;
        public FixVec2 Pos;                // 판정 중심(spec 01 5.6)
        public Dir8 Facing;                // 시야 = 발사 방향
        public Dir8 LastPressedCardinal;   // 자세 진입 때 대각 시야를 맞출 방향(E/N/W/S)
        public PendingDiag Pending;
        public StanceMode Stance;
        public Dir8 StanceLockedFacing;    // 자세 중 잠긴 시야(E/N/W/S)
        public KeyState Keys;
        public int HpPoints, MaxHpPoints;  // 1칸 = 100점(spec 01 6.1)
        public uint InvulnUntil;           // t < InvulnUntil 이면 무적
        public bool InvulnActive;          // 아직 InvulnEnded 를 내지 않은 무적이 걸려 있다
        public bool HasBeenHit;
        public uint LastHitTick;           // HasBeenHit 일 때 마지막으로 피격이 확정된 틱
        public PendingHit Hit;

        // 바이트: character u8, pos (x i64, y i64), facing u8, last_pressed_cardinal u8, pending(bool, i64, u8),
        //         stance u8, locked u8, keys(KeyState.Write), hp i32, max_hp i32, invuln_until u32, invuln_active bool,
        //         has_been_hit bool, last_hit_tick u32, hit(bool, i32, i32)
        public void Write(ref StateWriter w)
        {
            w.U8((byte)Character);
            w.Vec(Pos);
            w.U8((byte)Facing);
            w.U8((byte)LastPressedCardinal);
            Pending.Write(ref w);
            w.U8((byte)Stance);
            w.U8((byte)StanceLockedFacing);
            Keys.Write(ref w);
            w.I32(HpPoints);
            w.I32(MaxHpPoints);
            w.U32(InvulnUntil);
            w.Bool(InvulnActive);
            w.Bool(HasBeenHit);
            w.U32(LastHitTick);
            Hit.Write(ref w);
        }

        public void Read(ref StateReader r)
        {
            Character = (CharacterId)r.U8();
            Pos = r.Vec();
            Facing = ReadDir(ref r);
            LastPressedCardinal = ReadCardinal(ref r);
            Pending.Read(ref r);
            Stance = ReadStance(ref r);
            StanceLockedFacing = ReadCardinal(ref r);
            Keys.Read(ref r);
            HpPoints = r.I32();
            MaxHpPoints = r.I32();
            if (MaxHpPoints < 0 || HpPoints < 0 || HpPoints > MaxHpPoints)
            {
                throw new SimInvariantException("player: hp out of range");
            }
            InvulnUntil = r.U32();
            InvulnActive = r.Bool();
            HasBeenHit = r.Bool();
            LastHitTick = r.U32();
            Hit.Read(ref r);
            if (Stance != StanceMode.Normal && (Pending.Active || Facing != StanceLockedFacing))
            {
                throw new SimInvariantException("player: stance state is inconsistent");
            }
        }

        internal static Dir8 ReadDir(ref StateReader r)
        {
            byte v = r.U8();
            if (v > (byte)Dir8.SE)
            {
                throw new SimInvariantException("player: bad Dir8");
            }
            return (Dir8)v;
        }

        private static Dir8 ReadCardinal(ref StateReader r)
        {
            Dir8 d = ReadDir(ref r);
            if (PlayerRules.IsDiagonal(d))
            {
                throw new SimInvariantException("player: expected a cardinal direction");
            }
            return d;
        }

        private static StanceMode ReadStance(ref StateReader r)
        {
            byte v = r.U8();
            if (v > (byte)StanceMode.Reverse)
            {
                throw new SimInvariantException("player: bad StanceMode");
            }
            return (StanceMode)v;
        }
    }
}
