using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Player;

namespace Wizard.Sim
{
    // Heat 모듈(절 2). spec 01 7절(과열: 발사 열, 임계, 잠금, 냉각, 열 예외), 3.8(틱 순서), 17절(연결점).
    // 훅: Stage01_Heat(잠금 풀림), Stage06_Heat(J 발사), Stage11_Heat(냉각). 상태 훅 넷.
    //
    // 6단계의 순서: Stage06_Heat 가 발사 여부·임계·열·잠금을 정하고(HeatFire), 같은 단계의 Stage06_Spell 이
    // HeatFiredThisTick 으로 읽어 ISpellExecutor.Execute(isCrit, shotId)를 부른다(CONTRACTS 3.2 "과열 판정 → Execute → 열 반영").
    // 더하는 열은 Execute 의 HeatToAddHu 와 같은 값(spec 02 7.2)이라 판정 때 바로 더한다. 6단계가 끝난 뒤의 상태는 같다.
    //
    // 주문(발사 열과 간격)은 이 모듈이 정하지 않는다: 주문을 가진 쪽이 SetHeatShot 으로 넣는다(M2부터 Spell 모듈, M1은 시제품 내용).
    // 변이의 과열 수치도 변이 데이터를 읽는 쪽(Characters)이 SetHeatModifiers 로 넣는다. 둘 다 넣기 전에는 "주문 없음", "변이 없음"이다.
    //
    // 시작 상태(spec 01 3.7: 열 0, 잠금 없음)는 첫 Heat 훅(6단계) 또는 첫 Heat 연산이 한 번 세운다
    // [임시: 세계 생성 때의 초기화 훅이 계약에 없다, M1-SIM-02 결과 보고]. 세웠는지(_heatReady)는 상태다.
    // Player 모듈이 꺼진 팩(characters 절 없음)에서는 이 모듈도 꺼진다: 훅은 아무것도 하지 않고 절의 내용은 비어 있다.
    public sealed partial class SimWorld
    {
        private const int HeatWindowTicks = SimVersion.TickRate;   // 탐식의 "초당" 창(spec 01 7.6.5)

        // 상태(절 2). 색인 = 슬롯. 길이는 MaxPlayers 이고 0..PlayerCount-1 만 쓴다
        private readonly HeatState[] _heat = new HeatState[MaxPlayers];
        private readonly int[] _heatKillHu = new int[MaxPlayers * HeatWindowTicks];      // 탐식 60틱 창(플레이어마다 60칸)
        private readonly uint[] _heatKillTick = new uint[MaxPlayers * HeatWindowTicks];
        private bool _heatReady;
        private int _heatLastShotId;                    // 마지막으로 준 J 발사 id(세계 하나에 하나, 1부터)

        // 틱 안의 임시 값(상태 아님): 이번 틱 6단계의 발사 결과
        private readonly HeatFire[] _heatFires = new HeatFire[MaxPlayers];

        // ── 다른 모듈과 시험이 읽는 값(같은 어셈블리 안)

        internal bool HeatModuleOn => PlayerModuleOn;

        internal bool HeatReady => _heatReady;

        internal int HeatLastShotId => _heatLastShotId;

        internal ref readonly HeatState HeatAt(int index)
        {
            RequireHeatIndex(index);
            return ref _heat[index];
        }

        // spec 01 7.3 CritStartHu(p). 스침(8.4)이 읽는다
        internal int HeatCritStartHu(int index) => HeatAt(index).CritStartHu;

        // 표시 계층의 값(spec 01 17.1, CONTRACTS 3.3 PlayerView)
        internal HeatRingState HeatRingOf(int index)
        {
            ref readonly HeatState s = ref HeatAt(index);
            return HeatRules.Ring(s.HeatHu, s.CritStartHu, s.Locked);
        }

        internal int HeatLockRemainTicks(int index)
        {
            ref readonly HeatState s = ref HeatAt(index);
            if (!s.Locked)
            {
                return 0;
            }
            long remain = (long)s.UnlockTick - _tick;
            return remain > 0 ? (int)remain : 0;
        }

        internal int HeatLockTotalTicks(int index)
        {
            ref readonly HeatState s = ref HeatAt(index);
            return s.Locked ? s.LockTotalTicks : 0;
        }

        // 이번 틱 6단계에 이 플레이어가 J 로 쐈는가(Spell 의 Execute, Combo 의 불안정 핵 콤보 0, Characters 의 끓어넘침이 읽는다)
        internal bool HeatFiredThisTick(int index, out HeatFire fire)
        {
            RequireHeatIndex(index);
            fire = _heatFires[index];
            return fire.Kind == HeatFireKind.Fired && fire.Tick == _tick;
        }

        // ── 다른 모듈이 부르는 연산

        // 주문을 가진 쪽이 부른다: J 발사 1회의 열(HU, 거울상까지 적용, ShotHeat.ShotHeatHu)과 코어의 간격(틱)
        internal void SetHeatShot(PlayerSlot slot, BlockId core, int shotHeatHu, int intervalTicks)
        {
            if (shotHeatHu < 0 || intervalTicks < 1)
            {
                throw new SimInvariantException("heat: shot heat must be >= 0 and interval >= 1");
            }
            ref HeatState s = ref HeatFor(slot);
            s.Shot = new HeatShotProfile { Armed = true, Core = core, HeatHu = shotHeatHu, IntervalTicks = intervalTicks };
        }

        internal void ClearHeatShot(PlayerSlot slot) => HeatFor(slot).Shot = default;

        // 변이 데이터를 읽는 쪽(Characters)이 부른다(spec 01 7.6.3~7.6.5)
        internal void SetHeatModifiers(PlayerSlot slot, in HeatModifiers modifiers)
        {
            if (!modifiers.IsValid)
            {
                throw new SimInvariantException("heat: modifiers out of range");
            }
            HeatFor(slot).Mods = modifiers;
        }

        // 스쳐 피하기의 열 보상(spec 01 8.4): 잠금 해제(열 = 임계 시작) 또는 열을 임계 시작까지, 그리고 다음 발사 열 면제
        internal void HeatApplyGraze(PlayerSlot slot, ISimEventSink sink)
        {
            ref HeatState s = ref HeatFor(slot);
            if (HeatMachine.ApplyGraze(ref s, out bool armedNow))
            {
                EmitHeat(sink, SimEventType.HeatUnlocked, slot, (int)HeatUnlockReason.Graze, s.HeatHu);
            }
            if (armedNow)
            {
                EmitHeat(sink, SimEventType.HeatExemptArmed, slot, 0, 0);
            }
            NoteHeatRing(ref s, slot, sink);
        }

        // 방열 폭발(spec 01 7.6.7): 피해 계산에 쓸 열(정수, 잠금 중이면 100)을 돌려주고 열 0, 잠금이면 푼다
        internal int HeatConsumeForVentBlast(PlayerSlot slot, ISimEventSink sink)
        {
            ref HeatState s = ref HeatFor(slot);
            int heat = HeatMachine.ConsumeForVentBlast(ref s, out bool broke);
            if (broke)
            {
                EmitHeat(sink, SimEventType.HeatUnlocked, slot, (int)HeatUnlockReason.VentBlast, 0);
            }
            NoteHeatRing(ref s, slot, sink);
            return heat;
        }

        // 처치(10단계, 적 명세의 처치 이벤트)마다 처치한 플레이어 쪽에서 부른다. 탐식이 없으면 아무것도 하지 않는다(spec 01 7.6.5)
        internal int HeatOnKill(PlayerSlot slot)
        {
            ref HeatState s = ref HeatFor(slot);
            int i = slot.Value;
            return HeatMachine.OnKill(ref s, _tick,
                new Span<int>(_heatKillHu, i * HeatWindowTicks, HeatWindowTicks),
                new Span<uint>(_heatKillTick, i * HeatWindowTicks, HeatWindowTicks));
        }

        // 불가마 지대(spec 01 7.6.2)를 깔 때: 이전 지대는 사라지고 남은 흡수량을 이어받지 않는다(동시에 1개)
        internal void HeatStartKiln(PlayerSlot slot, FixVec2 center, Fix64 radius, int absorbHu)
        {
            if (radius < Fix64.Zero || absorbHu < 0)
            {
                throw new SimInvariantException("heat: kiln radius and absorb must be >= 0");
            }
            ref HeatState s = ref HeatFor(slot);
            s.KilnActive = true;
            s.KilnCenter = center;
            s.KilnRadius = radius;
            s.KilnAbsorbLeftHu = absorbHu;
        }

        // 지대가 사라지면 흡수량 0
        internal void HeatEndKiln(PlayerSlot slot)
        {
            ref HeatState s = ref HeatFor(slot);
            s.KilnActive = false;
            s.KilnCenter = FixVec2.Zero;
            s.KilnRadius = Fix64.Zero;
            s.KilnAbsorbLeftHu = 0;
        }

        // 결투 라운드 시작 같은 초기화(spec 01 3.7: 열 0, 잠금 없음). 주문·변이·수치는 그대로 둔다
        internal void HeatReset(PlayerSlot slot, ISimEventSink sink)
        {
            ref HeatState s = ref HeatFor(slot);
            bool wasLocked = s.Locked;
            HeatState kept = s;
            s = HeatState.Initial(_db.Character(PlayerAt(slot.Value).Character));
            s.Mods = kept.Mods;
            s.Shot = kept.Shot;
            int i = slot.Value;
            new Span<int>(_heatKillHu, i * HeatWindowTicks, HeatWindowTicks).Clear();
            new Span<uint>(_heatKillTick, i * HeatWindowTicks, HeatWindowTicks).Clear();
            if (wasLocked)
            {
                EmitHeat(sink, SimEventType.HeatUnlocked, slot, (int)HeatUnlockReason.Reset, 0);
            }
        }

        // ── 단계 훅

        // 1단계: 잠금 풀림 → 열 0(spec 01 7.5 ProcessUnlock)
        partial void Stage01_Heat(ISimEventSink sink)
        {
            if (!HeatModuleOn || !_heatReady)
            {
                return;                                  // 아직 아무도 잠기지 않았다
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                ref HeatState s = ref _heat[i];
                if (HeatMachine.ProcessUnlock(ref s, _tick))
                {
                    EmitHeat(sink, SimEventType.HeatUnlocked, SlotAt(i), (int)HeatUnlockReason.Timeout, 0);
                    NoteHeatRing(ref s, SlotAt(i), sink);
                }
            }
        }

        // 6단계: J 발사(spec 01 7.3 TryFire). 발사 방향·위치와 탄은 Spell 모듈(Execute)의 것이다
        partial void Stage06_Heat(ISimEventSink sink)
        {
            if (!HeatModuleOn)
            {
                return;
            }
            EnsureHeat();
            Array.Clear(_heatFires, 0, _heatFires.Length);
            long tickStart = SimTime.InputTime(_tick, 0);
            for (int i = 0; i < PlayerCount; i++)
            {
                ref HeatState s = ref _heat[i];
                ref readonly PlayerState p = ref PlayerAt(i);
                bool held = p.Keys.IsDown(LogicalKey.Fire);
                bool pressed = p.Keys.TryGetLastPress(LogicalKey.Fire, out long pressedAt) && pressedAt >= tickStart;
                bool inKiln = s.KilnActive && FixVec2.DistanceSq(p.Pos, s.KilnCenter) <= s.KilnRadius * s.KilnRadius;
                HeatMachine.TryFire(ref s, _tick, held, pressed, _config.FireEnabled, inKiln, _config.UnstableCoreSelfBlowup,
                                    ref HeldRng(RngStream.Mutation), ref _heatLastShotId, out _heatFires[i]);
                EmitFire(i, in _heatFires[i], in p, sink);
                NoteHeatRing(ref s, SlotAt(i), sink);
            }
        }

        // 11단계: 냉각(spec 01 7.4)
        partial void Stage11_Heat(ISimEventSink sink)
        {
            if (!HeatModuleOn)
            {
                return;
            }
            EnsureHeat();
            for (int i = 0; i < PlayerCount; i++)
            {
                ref HeatState s = ref _heat[i];
                HeatMachine.Cool(ref s, _tick);
                NoteHeatRing(ref s, SlotAt(i), sink);
            }
        }

        // 발사 한 번의 표시 이벤트(spec 01 17.2). 순서는 7.3 의 순서: 면제 → 불가마 → 발사 → 임계 → 잠금(→ 끓어넘침) → 자폭
        //   ShotFired: A = shotId, B = coreBlockId, C = isCrit(0/1), V = heatAfter(HU), X/Y = 플레이어 위치
        //   CritShotFired: A = shotId / HeatExemptConsumed: A = shotId / KilnAbsorbed: A = 먹은 양(HU), B = 남은 양(HU)
        //   HeatLockEntered: A = unlockTick, B = 잠금 전체 틱(간격 + 잠금) / BoiloverTriggered, UnstableCoreExploded: X/Y = 위치, A = shotId
        private void EmitFire(int i, in HeatFire f, in PlayerState p, ISimEventSink sink)
        {
            PlayerSlot slot = SlotAt(i);
            if (f.Kind == HeatFireKind.DryWhileLocked)
            {
                EmitHeat(sink, SimEventType.FireDryWhileLocked, slot, 0, 0);
                return;
            }
            if (f.Kind != HeatFireKind.Fired)
            {
                return;
            }
            ref readonly HeatState s = ref _heat[i];
            if (f.ExemptUsed)
            {
                EmitHeat(sink, SimEventType.HeatExemptConsumed, slot, f.ShotId, 0);
            }
            if (f.KilnAbsorbedHu > 0)
            {
                EmitHeat(sink, SimEventType.KilnAbsorbed, slot, f.KilnAbsorbedHu, s.KilnAbsorbLeftHu);
            }
            var shot = new SimEvent
            {
                Type = SimEventType.ShotFired, Slot = slot, Tick = _tick,
                A = f.ShotId, B = (int)f.Core, C = f.IsCrit ? 1 : 0, V = f.HeatAfterHu, X = p.Pos.X.Raw, Y = p.Pos.Y.Raw,
            };
            sink.Emit(in shot);
            if (f.IsCrit)
            {
                EmitHeat(sink, SimEventType.CritShotFired, slot, f.ShotId, 0);
            }
            if (f.LockEntered)
            {
                EmitHeat(sink, SimEventType.HeatLockEntered, slot, unchecked((int)s.UnlockTick), s.LockTotalTicks);
                if (s.Boilover)
                {
                    EmitAt(sink, SimEventType.BoiloverTriggered, slot, f.ShotId, in p);   // 피해·넉백은 비기·컴파일러 명세(7.6.6)
                }
            }
            if (f.UnstableBlowup)
            {
                EmitAt(sink, SimEventType.UnstableCoreExploded, slot, f.ShotId, in p);    // 피해는 컴파일러 명세(7.6.4)
            }
        }

        // 열 링이 임박·임계·넘치기 직전으로 바뀐 순간 한 번(spec 01 17.2 HeatEntered…). A = 그때의 열(HU)
        private void NoteHeatRing(ref HeatState s, PlayerSlot slot, ISimEventSink sink)
        {
            HeatRingState now = HeatRules.Ring(s.HeatHu, s.CritStartHu, s.Locked);
            if (now == s.Ring)
            {
                return;
            }
            s.Ring = now;
            switch (now)
            {
                case HeatRingState.Imminent:
                    EmitHeat(sink, SimEventType.HeatEnteredImminent, slot, s.HeatHu, 0);
                    break;
                case HeatRingState.Critical:
                    EmitHeat(sink, SimEventType.HeatEnteredCritical, slot, s.HeatHu, 0);
                    break;
                case HeatRingState.Brink:
                    EmitHeat(sink, SimEventType.HeatEnteredBrink, slot, s.HeatHu, 0);
                    break;
            }
        }

        private void EmitHeat(ISimEventSink sink, SimEventType type, PlayerSlot slot, int a, int b)
        {
            var e = new SimEvent { Type = type, Slot = slot, Tick = _tick, A = a, B = b };
            sink.Emit(in e);
        }

        private void EmitAt(ISimEventSink sink, SimEventType type, PlayerSlot slot, int a, in PlayerState p)
        {
            var e = new SimEvent { Type = type, Slot = slot, Tick = _tick, A = a, X = p.Pos.X.Raw, Y = p.Pos.Y.Raw };
            sink.Emit(in e);
        }

        // ── 시작 상태

        private ref HeatState HeatFor(PlayerSlot slot)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("heat: slot is not in this world");
            }
            if (!HeatModuleOn)
            {
                throw new SimInvariantException("heat: the pack has no characters (heat module is off)");
            }
            EnsureHeat();
            return ref _heat[slot.Value];
        }

        // 캐릭터 수치가 플레이어의 캐릭터에서 오므로 플레이어가 먼저 서야 한다. 아직이면 Player 모듈의 지연 초기화를 같이 부른다
        // (QueuePlayerHit 와 같은 길. 세계 생성 때의 초기화 훅이 생기면 둘 다 그리로 옮긴다). 판정 순서에는 닿지 않는다:
        // 시작 상태는 언제 세워도 같은 값이다
        private void EnsureHeat()
        {
            if (_heatReady)
            {
                return;
            }
            if (!PlayersSpawned)
            {
                EnsurePlayers();
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                _heat[i] = HeatState.Initial(_db.Character(PlayerAt(i).Character));
            }
            Array.Clear(_heatKillHu, 0, _heatKillHu.Length);
            Array.Clear(_heatKillTick, 0, _heatKillTick.Length);
            _heatLastShotId = 0;
            _heatReady = true;
        }

        private void RequireHeatIndex(int index)
        {
            if ((uint)index >= (uint)PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        // ── 상태 훅

        partial void Snapshot_Save_Heat(SimWorld target)
        {
            new ReadOnlySpan<HeatState>(_heat).CopyTo(target._heat);
            new ReadOnlySpan<int>(_heatKillHu).CopyTo(target._heatKillHu);
            new ReadOnlySpan<uint>(_heatKillTick).CopyTo(target._heatKillTick);
            target._heatReady = _heatReady;
            target._heatLastShotId = _heatLastShotId;
        }

        partial void Snapshot_Load_Heat(SimWorld source)
        {
            new ReadOnlySpan<HeatState>(source._heat).CopyTo(_heat);
            new ReadOnlySpan<int>(source._heatKillHu).CopyTo(_heatKillHu);
            new ReadOnlySpan<uint>(source._heatKillTick).CopyTo(_heatKillTick);
            _heatReady = source._heatReady;
            _heatLastShotId = source._heatLastShotId;
            Array.Clear(_heatFires, 0, _heatFires.Length);   // 다른 시간선의 임시 값을 남기지 않는다
        }

        // 내용(모듈이 켜져 있을 때만): ready bool, last_shot_id i32,
        //   플레이어마다(슬롯 순) HeatState.Write, 그리고 탐식 창 60칸(색인 순) × (hu i32, tick u32)
        partial void State_Write_Heat(ref StateWriter w)
        {
            if (!HeatModuleOn)
            {
                return;
            }
            w.Bool(_heatReady);
            w.I32(_heatLastShotId);
            for (int i = 0; i < PlayerCount; i++)
            {
                _heat[i].Write(ref w);
                int b = i * HeatWindowTicks;
                for (int k = 0; k < HeatWindowTicks; k++)
                {
                    w.I32(_heatKillHu[b + k]);
                    w.U32(_heatKillTick[b + k]);
                }
            }
        }

        partial void State_Read_Heat(ref StateReader r)
        {
            if (!HeatModuleOn)
            {
                return;
            }
            Array.Clear(_heatFires, 0, _heatFires.Length);
            _heatReady = r.Bool();
            _heatLastShotId = r.I32();
            for (int i = 0; i < PlayerCount; i++)
            {
                _heat[i].Read(ref r);
                int b = i * HeatWindowTicks;
                for (int k = 0; k < HeatWindowTicks; k++)
                {
                    int hu = r.I32();
                    if (hu < 0)
                    {
                        throw new SimInvariantException("heat: negative gluttony window entry");
                    }
                    _heatKillHu[b + k] = hu;
                    _heatKillTick[b + k] = r.U32();
                }
            }
        }
    }
}
