using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.3. 서버 → 클라. 고쳐 쓰는 버퍼다(확정 32틱 × 2슬롯 × 8개, dropped 255개를 미리 가진다).
    //
    //   u32 server_time_ms · u32 echo_client_send_time_ms · i16 margin_ms_estimate · u8 flags(1 해시, 2 버린 이벤트)
    //   u32 confirmed_from_tick · u8 confirmed_count(0..32)
    //   틱마다: u8 n0, u8 n1, 슬롯 0 의 이벤트 n0개, 슬롯 1 의 이벤트 n1개. 이벤트: u8 offset, u8 key_down, varuint seq(틱은 순서로 안다)
    //   [flags&1] u32 hash_tick, u64 state_hash
    //   [flags&2] u8 dropped_count(1..255), 항목: u32 seq, u8 reason, u16 run
    //
    // 플래그는 내용에서 정한다: 해시는 HasHash, 버린 이벤트는 DroppedCount > 0. 그래서 flags&2 인데 dropped_count 가 0 인 바이트는
    // 같은 바이트로 다시 쓸 수 없으므로 범위 밖으로 거절한다. 모르는 플래그 비트도 거절한다.
    // 서버가 B의 틱 T 입력을 A의 틱 T 봉인 뒤에만 싣는 규칙(미리 보기 막기)은 쓰는 쪽(DuelHostCore)이 지킨다.
    public sealed class ServerTickPacket
    {
        public const int FixedBytes = 16;
        public const int HashBytes = 12;
        public const int MaxDropped = 255;

        private const int MaxEvents = DuelWire.MaxConfirmedTicks * DuelWire.DuelSlots * DuelWire.MaxEventsPerTickPerSlot;

        private readonly InputEvent[] _events = new InputEvent[MaxEvents];
        private readonly int[] _start = new int[DuelWire.MaxConfirmedTicks];
        private readonly byte[] _n0 = new byte[DuelWire.MaxConfirmedTicks];
        private readonly byte[] _n1 = new byte[DuelWire.MaxConfirmedTicks];
        private readonly DroppedRun[] _dropped = new DroppedRun[MaxDropped];
        private int _ticks;
        private int _eventCount;
        private int _droppedCount;

        public uint ServerTimeMs;
        public uint EchoClientSendTimeMs;
        public short MarginMsEstimate;
        public uint ConfirmedFromTick;      // 아직 ack 받지 못한 첫 확정 틱. 틱을 더하기 전에 정한다
        public bool HasHash;
        public uint HashTick;
        public ulong StateHash;

        public int ConfirmedCount => _ticks;

        public int DroppedCount => _droppedCount;

        public ReadOnlySpan<DroppedRun> Dropped => new ReadOnlySpan<DroppedRun>(_dropped, 0, _droppedCount);

        public byte Flags => (byte)((HasHash ? DuelWire.ServerTickFlagHash : 0) | (_droppedCount > 0 ? DuelWire.ServerTickFlagDropped : 0));

        public void Clear()
        {
            ServerTimeMs = 0;
            EchoClientSendTimeMs = 0;
            MarginMsEstimate = 0;
            ConfirmedFromTick = 0;
            HasHash = false;
            HashTick = 0;
            StateHash = 0;
            ClearConfirmed();
            _droppedCount = 0;
        }

        public void ClearConfirmed()
        {
            _ticks = 0;
            _eventCount = 0;
        }

        public void ClearDropped() => _droppedCount = 0;

        // 틱 index(0부터)의 슬롯 이벤트. 틱 = ConfirmedFromTick + index
        public ReadOnlySpan<InputEvent> SlotEvents(int index, int slot)
        {
            if ((uint)index >= (uint)_ticks || (uint)slot >= DuelWire.DuelSlots)
            {
                return ReadOnlySpan<InputEvent>.Empty;
            }
            int start = _start[index] + (slot == 0 ? 0 : _n0[index]);
            int n = slot == 0 ? _n0[index] : _n1[index];
            return new ReadOnlySpan<InputEvent>(_events, start, n);
        }

        // 다음 확정 틱(ConfirmedFromTick + ConfirmedCount)을 더한다. 이벤트의 Tick 은 그 틱이어야 한다.
        // 32틱, 슬롯마다 8개, 결투 키, 순번 1 이상을 넘으면 더하지 않고 이유를 돌려준다
        public DuelCodecError TryAddConfirmedTick(ReadOnlySpan<InputEvent> slot0, ReadOnlySpan<InputEvent> slot1)
        {
            if (_ticks >= DuelWire.MaxConfirmedTicks || slot0.Length > DuelWire.MaxEventsPerTickPerSlot
                || slot1.Length > DuelWire.MaxEventsPerTickPerSlot || (uint)_ticks > uint.MaxValue - ConfirmedFromTick)
            {
                return DuelCodecError.OutOfRange;
            }
            uint tick = ConfirmedFromTick + (uint)_ticks;
            DuelCodecError e = CheckEvents(slot0, tick);
            if (e == DuelCodecError.None)
            {
                e = CheckEvents(slot1, tick);
            }
            if (e != DuelCodecError.None)
            {
                return e;
            }
            _start[_ticks] = _eventCount;
            _n0[_ticks] = (byte)slot0.Length;
            _n1[_ticks] = (byte)slot1.Length;
            slot0.CopyTo(new Span<InputEvent>(_events, _eventCount, slot0.Length));
            _eventCount += slot0.Length;
            slot1.CopyTo(new Span<InputEvent>(_events, _eventCount, slot1.Length));
            _eventCount += slot1.Length;
            _ticks++;
            return DuelCodecError.None;
        }

        public DuelCodecError TryAddDropped(in DroppedRun run)
        {
            if (_droppedCount >= MaxDropped || !run.IsValid)
            {
                return DuelCodecError.OutOfRange;
            }
            _dropped[_droppedCount++] = run;
            return DuelCodecError.None;
        }

        public DuelCodecError Measure(out int bodyBytes)
        {
            int size = FixedBytes;
            for (int i = 0; i < _eventCount; i++)
            {
                size += 2 + WireVarUInt.Size(_events[i].Seq);
            }
            size += 2 * _ticks;
            if (HasHash)
            {
                size += HashBytes;
            }
            if (_droppedCount > 0)
            {
                size += 1 + DroppedRun.WireBytes * _droppedCount;
            }
            if (size > DuelWire.MaxBodyBytes)
            {
                bodyBytes = 0;
                return DuelCodecError.TooLarge;   // 쓰는 쪽은 확정 틱이나 dropped 를 줄여 다음 패킷으로 넘긴다
            }
            bodyBytes = size;
            return DuelCodecError.None;
        }

        public DuelCodecError TryWrite(ByteWriter w)
        {
            DuelCodecError e = Measure(out _);
            if (e != DuelCodecError.None)
            {
                return e;
            }
            w.U32(ServerTimeMs);
            w.U32(EchoClientSendTimeMs);
            w.I16(MarginMsEstimate);
            w.U8(Flags);
            w.U32(ConfirmedFromTick);
            w.U8((byte)_ticks);
            for (int t = 0; t < _ticks; t++)
            {
                w.U8(_n0[t]);
                w.U8(_n1[t]);
                int end = _start[t] + _n0[t] + _n1[t];
                for (int i = _start[t]; i < end; i++)
                {
                    InputEvent ev = _events[i];
                    w.U8(ev.Offset);
                    w.U8(ev.PackKey());
                    w.VarUInt(ev.Seq);
                }
            }
            if (HasHash)
            {
                w.U32(HashTick);
                w.U64(StateHash);
            }
            if (_droppedCount > 0)
            {
                w.U8((byte)_droppedCount);
                for (int i = 0; i < _droppedCount; i++)
                {
                    DroppedRun d = _dropped[i];
                    w.U32(d.Seq);
                    w.U8((byte)d.Reason);
                    w.U16(d.Run);
                }
            }
            return DuelCodecError.None;
        }

        // 이 버퍼에 읽는다. 실패하면 Clear 된 상태로 남는다. 예외를 던지지 않는다
        public DuelCodecError TryRead(ReadOnlySpan<byte> body)
        {
            DuelCodecError e = ReadCore(body);
            if (e != DuelCodecError.None)
            {
                Clear();
            }
            return e;
        }

        private DuelCodecError ReadCore(ReadOnlySpan<byte> body)
        {
            Clear();
            if (body.Length > DuelWire.MaxBodyBytes)
            {
                return DuelCodecError.TooLarge;
            }
            var r = new ByteReader(body);
            if (!r.TryU32(out ServerTimeMs) || !r.TryU32(out EchoClientSendTimeMs) || !r.TryI16(out MarginMsEstimate)
                || !r.TryU8(out byte flags) || !r.TryU32(out ConfirmedFromTick) || !r.TryU8(out byte ticks))
            {
                return DuelCodecError.Truncated;
            }
            const int knownFlags = DuelWire.ServerTickFlagHash | DuelWire.ServerTickFlagDropped;
            if ((flags & ~knownFlags) != 0 || ticks > DuelWire.MaxConfirmedTicks
                || (ticks > 0 && (uint)(ticks - 1) > uint.MaxValue - ConfirmedFromTick))
            {
                return DuelCodecError.OutOfRange;
            }
            for (int t = 0; t < ticks; t++)
            {
                if (!r.TryU8(out byte n0) || !r.TryU8(out byte n1))
                {
                    return DuelCodecError.Truncated;
                }
                if (n0 > DuelWire.MaxEventsPerTickPerSlot || n1 > DuelWire.MaxEventsPerTickPerSlot)
                {
                    return DuelCodecError.OutOfRange;
                }
                uint tick = ConfirmedFromTick + (uint)t;
                _start[t] = _eventCount;
                _n0[t] = n0;
                _n1[t] = n1;
                int n = n0 + n1;
                for (int i = 0; i < n; i++)
                {
                    if (!r.TryU8(out byte offset) || !r.TryU8(out byte packed))
                    {
                        return DuelCodecError.Truncated;
                    }
                    DuelCodecError ve = WireVarUInt.Read32(ref r, out uint seq);
                    if (ve != DuelCodecError.None)
                    {
                        return ve;
                    }
                    if (seq == 0U || !DuelWire.TryUnpackDuelKey(packed, out LogicalKey key, out bool down))
                    {
                        return DuelCodecError.OutOfRange;
                    }
                    _events[_eventCount++] = new InputEvent(tick, offset, key, down, seq);
                }
                _ticks = t + 1;
            }
            if ((flags & DuelWire.ServerTickFlagHash) != 0)
            {
                if (!r.TryU32(out HashTick) || !r.TryU64(out StateHash))
                {
                    return DuelCodecError.Truncated;
                }
                HasHash = true;
            }
            if ((flags & DuelWire.ServerTickFlagDropped) != 0)
            {
                if (!r.TryU8(out byte count))
                {
                    return DuelCodecError.Truncated;
                }
                if (count == 0)
                {
                    return DuelCodecError.OutOfRange;
                }
                for (int i = 0; i < count; i++)
                {
                    if (!r.TryU32(out uint seq) || !r.TryU8(out byte reason) || !r.TryU16(out ushort run))
                    {
                        return DuelCodecError.Truncated;
                    }
                    var d = new DroppedRun(seq, (DuelDropReason)reason, run);
                    if (!d.IsValid)
                    {
                        return DuelCodecError.OutOfRange;
                    }
                    _dropped[_droppedCount++] = d;
                }
            }
            return r.Remaining == 0 ? DuelCodecError.None : DuelCodecError.TrailingBytes;
        }

        private static DuelCodecError CheckEvents(ReadOnlySpan<InputEvent> events, uint tick)
        {
            for (int i = 0; i < events.Length; i++)
            {
                InputEvent e = events[i];
                if (e.Tick != tick || e.Seq == 0U || !DuelWire.IsDuelKey(e.Key))
                {
                    return DuelCodecError.OutOfRange;
                }
            }
            return DuelCodecError.None;
        }
    }
}
