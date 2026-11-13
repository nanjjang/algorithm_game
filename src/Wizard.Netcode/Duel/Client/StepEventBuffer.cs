using System;
using Wizard.Sim.Events;

namespace Wizard.Netcode.Duel
{
    // 세계 하나의 Step 한 번이 낸 표시 이벤트를 모은다. 배열은 모자랄 때만 두 배로 늘린다(Step 의 할당이 아니라 이 버퍼의 일이다).
    internal sealed class StepEventBuffer : ISimEventSink
    {
        private SimEvent[] _events = new SimEvent[64];
        private int _count;

        public ReadOnlySpan<SimEvent> Events => new ReadOnlySpan<SimEvent>(_events, 0, _count);

        public void Begin() => _count = 0;

        public void Emit(in SimEvent e)
        {
            if (_count == _events.Length)
            {
                Array.Resize(ref _events, _events.Length * 2);
            }
            _events[_count++] = e;
        }
    }
}
