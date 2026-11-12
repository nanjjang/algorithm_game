using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.2. 클라 → 서버, 매 틱 1개 + 키 이벤트가 생기면 즉시 1개. 고쳐 쓰는 버퍼다(이벤트 64칸을 미리 가진다).
    //
    //   u32 sealed_through_tick · u32 ack_confirmed_tick · u32 client_send_time_ms · u32 echo_server_time_ms · u16 echo_hold_ms
    //   u32 base_seq · u8 event_count(0..64)
    //   이벤트[0]:   u32 tick, u8 offset, u8 key_down(아랫 4비트 = 키, 0x10 = 누름), u32 seq
    //   이벤트[i>0]: varuint Δtick, u8 offset, u8 key_down, varuint Δseq
    //
    // 코덱은 형식만 본다. 10.6절 (0)의 "순번이 늘지 않음", "같은 틱에서 오프셋 역행", "base_seq ≥ 첫 순번"은 형식으로 적을 수 있고
    // 호스트가 순서 위반(몰수 계수)으로 세야 하므로 여기서 거절하지 않는다(Δseq = 0 도 읽는다).
    // 형식으로 적을 수 없는 것(틱이나 순번이 앞보다 작다)은 쓰기에서 Unrepresentable 이다.
    public sealed class ClientInputPacket
    {
        public const int FixedBytes = 23;
        public const int FirstEventBytes = 10;

        private readonly InputEvent[] _events = new InputEvent[DuelWire.MaxClientEvents];
        private int _count;

        public uint SealedThroughTick;
        public uint AckConfirmedTick;
        public uint ClientSendTimeMs;
        public uint EchoServerTimeMs;
        public ushort EchoHoldMs;
        public uint BaseSeq;

        public int EventCount => _count;

        public ReadOnlySpan<InputEvent> Events => new ReadOnlySpan<InputEvent>(_events, 0, _count);

        public void Clear()
        {
            SealedThroughTick = 0;
            AckConfirmedTick = 0;
            ClientSendTimeMs = 0;
            EchoServerTimeMs = 0;
            EchoHoldMs = 0;
            BaseSeq = 0;
            _count = 0;
        }

        public void ClearEvents() => _count = 0;

        // 64개가 차면 false
        public bool TryAddEvent(in InputEvent e)
        {
            if (_count >= DuelWire.MaxClientEvents)
            {
                return false;
            }
            _events[_count++] = e;
            return true;
        }

        // 쓸 수 있는지 보고 본문 길이를 센다(쓰지 않는다)
        public DuelCodecError Measure(out int bodyBytes)
        {
            bodyBytes = 0;
            int size = FixedBytes;
            for (int i = 0; i < _count; i++)
            {
                InputEvent e = _events[i];
                if (!DuelWire.IsDuelKey(e.Key))
                {
                    return DuelCodecError.OutOfRange;
                }
                if (i == 0)
                {
                    size += FirstEventBytes;
                    continue;
                }
                InputEvent prev = _events[i - 1];
                if (e.Tick < prev.Tick || e.Seq < prev.Seq)
                {
                    return DuelCodecError.Unrepresentable;
                }
                size += WireVarUInt.Size(e.Tick - prev.Tick) + 2 + WireVarUInt.Size(e.Seq - prev.Seq);
            }
            if (size > DuelWire.MaxBodyBytes)
            {
                return DuelCodecError.TooLarge;   // 64개로는 닿지 않는다(최대 23 + 10 + 63 × 12 = 789). 방어용
            }
            bodyBytes = size;
            return DuelCodecError.None;
        }

        // w 의 끝에 본문을 붙인다. 실패하면 아무것도 쓰지 않는다
        public DuelCodecError TryWrite(ByteWriter w)
        {
            DuelCodecError e = Measure(out _);
            if (e != DuelCodecError.None)
            {
                return e;
            }
            w.U32(SealedThroughTick);
            w.U32(AckConfirmedTick);
            w.U32(ClientSendTimeMs);
            w.U32(EchoServerTimeMs);
            w.U16(EchoHoldMs);
            w.U32(BaseSeq);
            w.U8((byte)_count);
            for (int i = 0; i < _count; i++)
            {
                InputEvent ev = _events[i];
                if (i == 0)
                {
                    w.U32(ev.Tick);
                    w.U8(ev.Offset);
                    w.U8(ev.PackKey());
                    w.U32(ev.Seq);
                    continue;
                }
                InputEvent prev = _events[i - 1];
                w.VarUInt(ev.Tick - prev.Tick);
                w.U8(ev.Offset);
                w.U8(ev.PackKey());
                w.VarUInt(ev.Seq - prev.Seq);
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
            if (!r.TryU32(out SealedThroughTick) || !r.TryU32(out AckConfirmedTick) || !r.TryU32(out ClientSendTimeMs)
                || !r.TryU32(out EchoServerTimeMs) || !r.TryU16(out EchoHoldMs) || !r.TryU32(out BaseSeq) || !r.TryU8(out byte count))
            {
                return DuelCodecError.Truncated;
            }
            if (count > DuelWire.MaxClientEvents)
            {
                return DuelCodecError.OutOfRange;
            }
            uint tick = 0;
            uint seq = 0;
            for (int i = 0; i < count; i++)
            {
                byte offset;
                byte packed;
                if (i == 0)
                {
                    if (!r.TryU32(out tick) || !r.TryU8(out offset) || !r.TryU8(out packed) || !r.TryU32(out seq))
                    {
                        return DuelCodecError.Truncated;
                    }
                }
                else
                {
                    DuelCodecError ve = WireVarUInt.Read32(ref r, out uint dTick);
                    if (ve != DuelCodecError.None)
                    {
                        return ve;
                    }
                    if (!r.TryU8(out offset) || !r.TryU8(out packed))
                    {
                        return DuelCodecError.Truncated;
                    }
                    ve = WireVarUInt.Read32(ref r, out uint dSeq);
                    if (ve != DuelCodecError.None)
                    {
                        return ve;
                    }
                    if (dTick > uint.MaxValue - tick || dSeq > uint.MaxValue - seq)
                    {
                        return DuelCodecError.OutOfRange;   // 32비트를 넘는 틱·순번
                    }
                    tick += dTick;
                    seq += dSeq;
                }
                if (!DuelWire.TryUnpackDuelKey(packed, out LogicalKey key, out bool down))
                {
                    return DuelCodecError.OutOfRange;
                }
                _events[_count++] = new InputEvent(tick, offset, key, down, seq);
            }
            return r.Remaining == 0 ? DuelCodecError.None : DuelCodecError.TrailingBytes;
        }
    }
}
