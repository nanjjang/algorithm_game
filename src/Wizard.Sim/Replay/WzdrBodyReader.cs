using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 11.1 결투 확정 기록(WZDR)의 본문: 0x05 DuelInput(varuint Δtick, u8 slot, u8 offset, u8 key_down, varuint seq)의 열 + 꼬리말.
    // 결투에는 0단계 명령과 일시정지가 없으므로 0x05 밖의 꼬리표는 모르는 꼬리표다.
    // 순서: 레코드 순서 = 확정 열의 순서 = InputOrder 순(틱, 오프셋, 분류, 슬롯, 순번). 같은 것 둘은 없다(Step 의 전제와 같다).
    // 순번: 슬롯마다 1 이상이고 늘어나기만 한다. 건너뜀은 된다(서버가 유실로 소비한 순번, CONTRACTS 10.6).
    // 슬롯마다 한 틱에 8개까지. 슬롯은 0·1(결투는 두 사람).
    internal ref struct WzdrBodyReader
    {

        private ByteReader _r;
        private bool _any;
        private uint _base;
        private RecordEntry _last;          // 같은 틱 안의 순서 비교용
        private int _keys0, _keys1;         // 지금 틱의 슬롯별 키 수
        private uint _seq0, _seq1;          // 슬롯별 마지막 순번(0이면 없음)

        public WzdrBodyReader(ReadOnlySpan<byte> body)
        {
            _r = new ByteReader(body);
            _any = false;
            _base = 0;
            _last = default;
            _keys0 = 0;
            _keys1 = 0;
            _seq0 = 0;
            _seq1 = 0;
            FooterStart = -1;
        }

        public int FooterStart { get; private set; }

        public bool Next(out RecordEntry entry, out RecordError error)
        {
            entry = default;
            int at = _r.Position;
            if (!_r.TryU8(out byte tag))
            {
                error = RecordError.Truncated;
                return false;
            }
            if (tag == InputRecordFormat.TagFooter)
            {
                FooterStart = at;
                error = RecordError.None;
                return false;
            }
            if (tag != InputRecordFormat.TagDuelInput)
            {
                error = RecordError.UnknownTag;
                return false;
            }
            error = InputRecordFormat.ReadTick(ref _r, _base, out uint tick);
            if (error != RecordError.None)
            {
                return false;
            }
            if (!_r.TryU8(out byte slot) || !_r.TryU8(out byte offset) || !_r.TryU8(out byte packed))
            {
                error = RecordError.Truncated;
                return false;
            }
            if (!_r.TryVarUInt(out ulong seqWide))
            {
                // 끝에서 모자라면 잘린 것, 아니면 64비트를 넘친 순번
                error = InputRecordFormat.VarUIntFailure(ref _r) == RecordError.Truncated ? RecordError.Truncated : RecordError.BadSeq;
                return false;
            }
            if (slot >= InputRecordFormat.DuelSlots)
            {
                error = RecordError.BadSlot;
                return false;
            }
            if (!InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down))
            {
                error = RecordError.BadKey;
                return false;
            }
            uint lastSeq = slot == 0 ? _seq0 : _seq1;
            if (seqWide == 0UL || seqWide > uint.MaxValue || (uint)seqWide <= lastSeq)
            {
                error = RecordError.BadSeq;   // 순번은 슬롯마다 1 이상이고 늘어나기만 한다
                return false;
            }

            entry.Kind = RecordKind.DuelInput;
            entry.Tick = tick;
            entry.Slot = slot;
            entry.Offset = offset;
            entry.Key = key;
            entry.Down = down;
            entry.Seq = (uint)seqWide;

            bool newTick = !_any || tick != _base;
            if (newTick)
            {
                _keys0 = 0;
                _keys1 = 0;
            }
            else if (InputOrder.Compare(ToInput(in _last), ToInput(in entry)) >= 0)
            {
                error = RecordError.KeyOrder;
                return false;
            }
            int count = (slot == 0 ? _keys0 : _keys1) + 1;
            if (count > InputOrder.MaxEventsPerTickPerPlayer)
            {
                error = RecordError.TooManyKeys;
                return false;
            }
            if (slot == 0)
            {
                _keys0 = count;
                _seq0 = entry.Seq;
            }
            else
            {
                _keys1 = count;
                _seq1 = entry.Seq;
            }
            _any = true;
            _base = tick;
            _last = entry;
            return true;
        }

        public RecordError CheckAgainstEnd(uint endTick, uint maxTicks)
        {
            if (endTick > maxTicks)
            {
                return RecordError.EndTickOverLimit;
            }
            if (_any && _base >= endTick)
            {
                return RecordError.TickOverflow;
            }
            return RecordError.None;
        }

        public static PlayerInput ToInput(in RecordEntry e) =>
            new PlayerInput(new PlayerSlot(e.Slot), new InputEvent(e.Tick, e.Offset, e.Key, e.Down, e.Seq));
    }
}
