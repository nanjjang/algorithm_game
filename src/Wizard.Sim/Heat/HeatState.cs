using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.Sim.Heat
{
    // spec 01 3.9 PlayerState 중 과열(절 2)의 필드와 그 플레이어의 과열 수치. 값 형식이고 참조가 없다.
    // 스냅숏은 값 복사, 직렬화는 Write/Read 의 순서 그대로다. 탐식의 60틱 창(GluttonyWindow)은 세계의 배열에 따로 있다.
    internal struct HeatState
    {
        // ── 캐릭터 수치(CharacterDef, spec 01 7.1). 판 안에서 바뀌지 않지만 상태에 두어 해시가 수치까지 덮는다
        public int CharCritStartHu;        // 임계 시작(달구 9,600, 성에 10,800)
        public int CoolHuPerTick;          // 냉각(기준 90, 초롱 100)
        public int CoolDelayTicks;         // 냉각 대기(12)
        public int LockTicks;              // 잠금(기준 180, 초롱 150, 갈피 210)
        public bool Boilover;              // 패시브 끓어넘침(달구): 잠금에 들어갈 때 BoiloverTriggered(7.6.6)

        public HeatModifiers Mods;         // 변이(7.6.3~7.6.5)
        public HeatShotProfile Shot;       // 지금 J 가 쏘는 주문

        // ── 게이지
        public int HeatHu;                 // 0 ~ 12,000 + 한 발 최대 열
        public bool Locked;
        public uint UnlockTick;            // Locked 일 때: t >= UnlockTick 이면 1단계에서 풀린다
        public uint LockStartTick;         // Locked 일 때: 잠금에 들어간 틱
        public int LockTotalTicks;         // Locked 일 때: 간격 + 잠금(표시의 LockTotalTicks)

        // ── 발사와 냉각
        public bool HasFired;
        public uint LastFireTick;          // HasFired 일 때 마지막 J 발사 틱
        public int LastFireIntervalTicks;  // 그 발사의 간격
        public uint FireReadyTick;         // 다음 J 발사가 가능한 틱

        // ── 예외
        public bool ExemptArmed;           // 다음 발사 1회 열 면제(8.4)
        public bool KilnActive;            // 불가마 지대(7.6.2)가 있다
        public FixVec2 KilnCenter;
        public Fix64 KilnRadius;
        public int KilnAbsorbLeftHu;       // 0 ~ 1,920

        public HeatRingState Ring;         // 마지막으로 알린 열 링 상태(HeatEntered… 이벤트를 한 번씩만 낸다)

        public int CritStartHu => HeatRules.CritStartHu(CharCritStartHu, Mods.CritStartCapHu);

        public static HeatState Initial(CharacterDef def)
        {
            return new HeatState
            {
                CharCritStartHu = def.CritStartHu,
                CoolHuPerTick = def.CoolHuPerTick,
                CoolDelayTicks = def.CoolDelayTicks,
                LockTicks = def.LockTicks,
                Boilover = def.Passive == PassiveId.Overflow,
                Mods = HeatModifiers.None,
                Ring = HeatRingState.Cold,
            };
        }

        // 바이트: char_crit i32, cool i32, cool_delay i32, lock i32, boilover bool, mods(HeatModifiers.Write), shot(HeatShotProfile.Write),
        //         heat i32, locked bool, unlock_tick u32, lock_start u32, lock_total i32,
        //         has_fired bool, last_fire u32, last_interval i32, fire_ready u32,
        //         exempt bool, kiln_active bool, kiln_center (x i64, y i64), kiln_radius i64, kiln_left i32, ring u8
        public void Write(ref StateWriter w)
        {
            w.I32(CharCritStartHu);
            w.I32(CoolHuPerTick);
            w.I32(CoolDelayTicks);
            w.I32(LockTicks);
            w.Bool(Boilover);
            Mods.Write(ref w);
            Shot.Write(ref w);
            w.I32(HeatHu);
            w.Bool(Locked);
            w.U32(UnlockTick);
            w.U32(LockStartTick);
            w.I32(LockTotalTicks);
            w.Bool(HasFired);
            w.U32(LastFireTick);
            w.I32(LastFireIntervalTicks);
            w.U32(FireReadyTick);
            w.Bool(ExemptArmed);
            w.Bool(KilnActive);
            w.Vec(KilnCenter);
            w.Fix(KilnRadius);
            w.I32(KilnAbsorbLeftHu);
            w.U8((byte)Ring);
        }

        public void Read(ref StateReader r)
        {
            CharCritStartHu = r.I32();
            CoolHuPerTick = r.I32();
            CoolDelayTicks = r.I32();
            LockTicks = r.I32();
            Boilover = r.Bool();
            Mods.Read(ref r);
            Shot.Read(ref r);
            HeatHu = r.I32();
            Locked = r.Bool();
            UnlockTick = r.U32();
            LockStartTick = r.U32();
            LockTotalTicks = r.I32();
            HasFired = r.Bool();
            LastFireTick = r.U32();
            LastFireIntervalTicks = r.I32();
            FireReadyTick = r.U32();
            ExemptArmed = r.Bool();
            KilnActive = r.Bool();
            KilnCenter = r.Vec();
            KilnRadius = r.Fix();
            KilnAbsorbLeftHu = r.I32();
            byte ring = r.U8();
            if (ring > (byte)HeatRingState.Locked)
            {
                throw new SimInvariantException("heat: bad HeatRingState");
            }
            Ring = (HeatRingState)ring;
            if (CoolHuPerTick < 0 || CoolDelayTicks < 0 || LockTicks < 0 || HeatHu < 0 || LockTotalTicks < 0
                || LastFireIntervalTicks < 0 || KilnAbsorbLeftHu < 0 || KilnRadius < Fix64.Zero
                || (!KilnActive && KilnAbsorbLeftHu != 0))
            {
                throw new SimInvariantException("heat: state out of range");
            }
        }
    }
}
