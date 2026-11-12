using System;
using Wizard.Sim.Events;

namespace Wizard.Client.Run
{
    // ARCHITECTURE 6.1·9절. 한 프레임 동안 세계가 낸 표시 이벤트를 모아 둔다(PresentationEventRouter.Drain(session.Events)).
    // 세션이 프레임 처음(OnInput)에 비운다. 버퍼는 이 객체가 가지고 모자랄 때만 늘린다.
    public sealed class SimEventBuffer : ISimEventSink
    {
        private const int InitialCapacity = 64;

        private SimEvent[] _items = new SimEvent[InitialCapacity];
        private int _count;

        public int Count => _count;

        public ReadOnlySpan<SimEvent> Events => new ReadOnlySpan<SimEvent>(_items, 0, _count);

        public void Emit(in SimEvent e)
        {
            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }
            _items[_count++] = e;
        }

        public void Clear() => _count = 0;
    }
}
