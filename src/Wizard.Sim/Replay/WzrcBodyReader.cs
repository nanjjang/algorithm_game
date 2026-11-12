using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 3.8 "신뢰할 수 없는 기록을 거르는 규칙", 11.1. .wzrc 본문(머리말 뒤의 바이트)을 레코드 하나씩 읽으며 구조를 확인한다.
    // InputRecordCodec(검사)과 ReplayRunner(재실행)가 이 하나를 쓴다. 검사와 재실행이 다른 규칙으로 읽을 수 없다.
    //
    // 틱: 0x01·0x02·0x03 은 앞 레코드의 틱 + Δtick, 0x04 는 절대 틱이다. 넷 모두 기준 틱을 옮긴다. 틱은 줄지 않는다.
    // 한 틱(묶음) 안: 명령(0x02)이 먼저, 그다음 키(0x01)가 (offset, KeyClass) 가 줄지 않는 순서로 8개까지. 일시정지(0x03)는 어디든 된다.
    // 중간 지점(0x04)의 해시는 그 틱을 Step 하기 전의 것이므로, 그 틱의 명령·키보다 먼저 와야 한다.
    // 순번: 명령과 키가 같은 공간에서 레코드 순서대로 1부터(3.1절). 0x03·0x04 는 순번을 쓰지 않는다.
    // end_tick 과의 비교(명령·키의 틱 < end_tick, 일시정지·중간 지점의 틱 ≤ end_tick)는 꼬리말을 읽은 뒤에 CheckAgainstEnd 로 한다.
    // LEB128 의 비정규 표기(값이 같은 더 긴 바이트)는 같은 값으로 읽는다. 바이트의 무결성은 record_sha256 이 본다.
    internal ref struct WzrcBodyReader
    {
        private ByteReader _r;
        private bool _any;              // 레코드를 하나라도 읽었는가
        private uint _base;             // 마지막 레코드의 틱 = 지금 묶음의 틱
        private int _groupKeys;
        private bool _groupHasWorld;    // 지금 묶음에 명령·키가 있는가
        private byte _lastOffset;
        private int _lastClass;
        private uint _seq;
        private bool _anyWorld;
        private uint _lastWorldTick;

        public WzrcBodyReader(ReadOnlySpan<byte> body)
        {
            _r = new ByteReader(body);
            _any = false;
            _base = 0;
            _groupKeys = 0;
            _groupHasWorld = false;
            _lastOffset = 0;
            _lastClass = 0;
            _seq = 0;
            _anyWorld = false;
            _lastWorldTick = 0;
            FooterStart = -1;
        }

        // 꼬리표 0xFF 의 자리(본문 안에서). 꼬리말을 만나기 전에는 −1
        public int FooterStart { get; private set; }

        // 레코드 하나를 읽으면 true. 꼬리말 꼬리표를 만나면 false 이고 error == None(reader 는 꼬리말 칸 앞에 있다). 구조 오류면 false 와 그 까닭
        public bool Next(out RecordEntry entry, out RecordError error)
        {
            entry = default;
            error = RecordError.None;
            int at = _r.Position;
            if (!_r.TryU8(out byte tag))
            {
                error = RecordError.Truncated;   // 꼬리말이 없다
                return false;
            }
            uint tick;
            switch (tag)
            {
                case InputRecordFormat.TagFooter:
                    FooterStart = at;
                    return false;
                case InputRecordFormat.TagInput:
                case InputRecordFormat.TagCommand:
                case InputRecordFormat.TagPause:
                    error = InputRecordFormat.ReadTick(ref _r, _base, out tick);
                    if (error != RecordError.None)
                    {
                        return false;
                    }
                    break;
                case InputRecordFormat.TagCheckpoint:
                    if (!_r.TryU16(out ushort wave) || !_r.TryU32(out tick) || !_r.TryU64(out ulong hash))
                    {
                        error = RecordError.Truncated;
                        return false;
                    }
                    if (_any && tick < _base)
                    {
                        error = RecordError.TickBackwards;
                        return false;
                    }
                    if (_any && tick == _base && _groupHasWorld)
                    {
                        error = RecordError.TickBackwards;   // 이미 기록된 그 틱의 입력보다 뒤의 해시
                        return false;
                    }
                    entry.Wave = wave;
                    entry.StateHash = hash;
                    break;
                default:
                    error = RecordError.UnknownTag;
                    return false;
            }

            if (!_any || tick != _base)
            {
                _groupKeys = 0;
                _groupHasWorld = false;
            }
            entry.Tick = tick;

            switch (tag)
            {
                case InputRecordFormat.TagInput:
                    if (!ReadInput(ref entry, out error))
                    {
                        return false;
                    }
                    break;
                case InputRecordFormat.TagCommand:
                    if (!ReadCommand(ref entry, out error))
                    {
                        return false;
                    }
                    break;
                case InputRecordFormat.TagPause:
                    if (!_r.TryU32(out uint wallMs))
                    {
                        error = RecordError.Truncated;
                        return false;
                    }
                    entry.Kind = RecordKind.Pause;
                    entry.WallMs = wallMs;
                    break;
                default:
                    entry.Kind = RecordKind.Checkpoint;
                    break;
            }

            _any = true;
            _base = tick;
            return true;
        }

        // 꼬리말의 end_tick 과 대조한다. maxTicks = ReplayLimits.MaxTicks(모르면 uint.MaxValue)
        public RecordError CheckAgainstEnd(uint endTick, uint maxTicks)
        {
            if (endTick > maxTicks)
            {
                return RecordError.EndTickOverLimit;
            }
            if (_anyWorld && _lastWorldTick >= endTick)
            {
                return RecordError.TickOverflow;   // 처리되지 않을 틱의 입력
            }
            if (_any && _base > endTick)
            {
                return RecordError.TickOverflow;
            }
            return RecordError.None;
        }

        private bool ReadInput(ref RecordEntry entry, out RecordError error)
        {
            error = RecordError.None;
            if (!_r.TryU8(out byte offset) || !_r.TryU8(out byte packed))
            {
                error = RecordError.Truncated;
                return false;
            }
            if (!InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down))
            {
                error = RecordError.BadKey;
                return false;
            }
            int cls = InputOrder.KeyClass(key);
            if (_groupKeys >= InputOrder.MaxEventsPerTickPerPlayer)
            {
                error = RecordError.TooManyKeys;
                return false;
            }
            if (_groupKeys > 0 && (offset < _lastOffset || (offset == _lastOffset && cls < _lastClass)))
            {
                error = RecordError.KeyOrder;
                return false;
            }
            if (!NextSeq(out uint seq, out error))
            {
                return false;
            }
            _groupKeys++;
            _lastOffset = offset;
            _lastClass = cls;
            MarkWorld(entry.Tick);
            entry.Kind = RecordKind.Input;
            entry.Offset = offset;
            entry.Key = key;
            entry.Down = down;
            entry.Seq = seq;
            return true;
        }

        private bool ReadCommand(ref RecordEntry entry, out RecordError error)
        {
            error = RecordError.None;
            if (!_r.TryU8(out byte op) || !_r.TryI32(out int a) || !_r.TryI32(out int b))
            {
                error = RecordError.Truncated;
                return false;
            }
            if (!InputRecordFormat.IsKnownOp(op))
            {
                error = RecordError.BadOp;
                return false;
            }
            if (_groupKeys > 0)
            {
                error = RecordError.CommandAfterKey;
                return false;
            }
            if (!NextSeq(out uint seq, out error))
            {
                return false;
            }
            MarkWorld(entry.Tick);
            entry.Kind = RecordKind.Command;
            entry.Op = (RunCommandOp)op;
            entry.A = a;
            entry.B = b;
            entry.Seq = seq;
            return true;
        }

        private bool NextSeq(out uint seq, out RecordError error)
        {
            if (_seq == uint.MaxValue)
            {
                seq = 0;
                error = RecordError.BadSeq;   // 순번 공간을 다 썼다
                return false;
            }
            _seq++;
            seq = _seq;
            error = RecordError.None;
            return true;
        }

        private void MarkWorld(uint tick)
        {
            _groupHasWorld = true;
            _anyWorld = true;
            _lastWorldTick = tick;
        }
    }
}
