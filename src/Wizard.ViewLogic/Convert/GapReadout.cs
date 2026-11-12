using System;
using Wizard.Content;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Convert
{
    // 표시 이벤트에서 슬롯마다 마지막 섬 간격 보고를 모은다(표시 전용 상태. 세계로 돌아가지 않는다).
    // 이벤트는 틱 순으로 온다(FixedTickDriver 의 OnEvents). Version 은 무엇이 바뀔 때마다 1씩 는다(글자를 다시 만들지 정하는 데 쓴다).
    public sealed class GapReadout
    {
        public const int SlotCapacity = 4;                // 자료구조 크기(PlayerSlot 0..3, CONTRACTS 1.5)

        private readonly GapReport[] _last = new GapReport[SlotCapacity];
        private readonly bool[] _has = new bool[SlotCapacity];

        public int Version { get; private set; }

        public void Observe(ReadOnlySpan<SimEvent> events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                Observe(in events[i]);
            }
        }

        public void Observe(in SimEvent e)
        {
            int slot = e.Slot.Value;
            if (slot >= SlotCapacity)
            {
                return;
            }
            switch (e.Type)
            {
                case SimEventType.PracticeGapReport:
                    _last[slot] = new GapReport(e.Tick, e.V >= 0, e.V, (ArtId)e.B, e.C != 0, false);
                    _has[slot] = true;
                    Version++;
                    break;
                case SimEventType.ArtCast:
                    // ArtCast: A = 기술, C = 섬(0/1). 같은 틱의 보고에 붙인다(보관된 K 가 뒤 틱에 나간 것은 붙이지 않는다)
                    if (_has[slot] && _last[slot].Tick == e.Tick && _last[slot].Decided && (int)_last[slot].Art == e.A)
                    {
                        _last[slot] = _last[slot].WithJust(e.C != 0);
                        Version++;
                    }
                    break;
            }
        }

        public bool TryGet(int slot, out GapReport report)
        {
            if ((uint)slot >= SlotCapacity || !_has[slot])
            {
                report = default;
                return false;
            }
            report = _last[slot];
            return true;
        }

        public void Clear()
        {
            Array.Clear(_has, 0, _has.Length);
            Version++;
        }
    }
}
