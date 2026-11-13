using System;
using System.Collections.Generic;
using Wizard.Sim.Input;

namespace Wizard.Client.Duel
{
    // SplitKeyboardInput 의 한 사람 몫 입력원(CONTRACTS 6.5 IInputSource). 그 사람의 키 배치로 읽은 논리 키 변화만 쌓는다
    internal sealed class SplitKeyboardPlayer : IInputSource
    {
        private readonly LogicalKeyTracker _tracker;
        private readonly Queue<RawKeyEvent> _pending = new Queue<RawKeyEvent>();
        private readonly RawKeyEvent[] _scratch = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
        private double _lastSeconds;

        public SplitKeyboardPlayer(KeyBindings bindings)
        {
            _tracker = new LogicalKeyTracker(bindings);
        }

        public KeyBindings Bindings => _tracker.Bindings;

        public KeyMask CurrentKeys => _tracker.Current;

        public bool DuelPolling { get; private set; }

        public void OnPhysicalKey(PhysicalKey key, bool down, double timeSeconds)
        {
            _lastSeconds = timeSeconds;
            Push(_tracker.OnPhysicalKey(key, down, timeSeconds, _scratch));
        }

        public int Drain(Span<RawKeyEvent> buffer)
        {
            int n = 0;
            while (n < buffer.Length && _pending.Count > 0)
            {
                buffer[n++] = _pending.Dequeue();
            }
            return n;
        }

        // 누르고 있는 물리 키는 새 배치로 다시 읽는다(차이는 마지막 키 시각의 이벤트로 낸다)
        public void SetBindings(KeyBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            Push(_tracker.SetBindings(bindings, _lastSeconds, _scratch));
        }

        public void SetDuelPolling(bool on) => DuelPolling = on;

        private void Push(int n)
        {
            for (int i = 0; i < n; i++)
            {
                _pending.Enqueue(_scratch[i]);
            }
        }
    }
}
