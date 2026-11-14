using System.Collections.Generic;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 각본 입력원(튜토리얼·PlayMode 테스트용). 각본 = (시각 u, 논리 키, 누름/뗌)의 목록이고, 시각이 된 줄부터 "가상 물리 키"를 바꾼다.
    /// 이벤트는 KeyboardInputSource와 같은 규칙(LogicalKeyTracker)으로 낸다: 논리 상태가 바뀔 때만, Resync의 삼킴, ReleaseAll.
    /// 시각은 지금 도는 세계의 틱 기준(틱 t = t × 256u). 늦게 읽힌 줄(그 시각이 이미 지난 Poll)은 지금 시각(simTimeU)으로 낸다
    /// — 세계는 지난 틱의 입력을 받지 않으므로.
    /// 같은 Poll 안에서 눌렀다 뗀 줄도 둘 다 낸다(한 줄씩 적용).
    /// </summary>
    public sealed class BotInputSource : IInputSource
    {
        struct Entry
        {
            public long TimeU;
            public LogicalKey Key;
            public bool Down;
            public int Order;
        }

        readonly List<Entry> _script = new List<Entry>();
        readonly bool[] _physical = new bool[SimConst.LogicalKeyCount];
        readonly LogicalKeyTracker _tracker = new LogicalKeyTracker();
        int _cursor;
        int _order;
        bool _sorted = true;

        /// <summary>각본을 다 썼나.</summary>
        public bool Finished => _cursor >= _script.Count;
        public int Remaining => _script.Count - _cursor;
        /// <summary>가상 물리 키 상태(각본이 지금까지 적용한 것).</summary>
        public bool IsPhysicallyDown(LogicalKey key) => _physical[(int)key];

        /// <summary>시각 u에 키를 누르거나 뗀다.</summary>
        public BotInputSource At(long timeU, LogicalKey key, bool down)
        {
            if (_script.Count > _cursor && timeU < _script[_script.Count - 1].TimeU) _sorted = false;
            _script.Add(new Entry { TimeU = timeU, Key = key, Down = down, Order = _order++ });
            return this;
        }

        /// <summary>틱 tick(+ 틱 안 오프셋 u)에 누른다.</summary>
        public BotInputSource Press(int tick, LogicalKey key, int offsetU = 0) => At(SimConst.TickStartU(tick) + offsetU, key, true);
        public BotInputSource Release(int tick, LogicalKey key, int offsetU = 0) => At(SimConst.TickStartU(tick) + offsetU, key, false);
        /// <summary>틱 tick에 누르고 holdTicks 틱 뒤에 뗀다.</summary>
        public BotInputSource Tap(int tick, LogicalKey key, int holdTicks = 1) => Press(tick, key).Release(tick + holdTicks, key);
        /// <summary>다음 Poll(또는 Resync)에서 바로 적용할 줄(시각 = long.MinValue, 늘 이미 지난 시각이라 지금 시각으로 나간다).</summary>
        public BotInputSource Now(LogicalKey key, bool down) => At(long.MinValue, key, down);

        /// <summary>각본과 가상 물리 상태를 비운다(내부 논리 상태는 남는다 — 떼려면 ReleaseAll).</summary>
        public void ClearScript()
        {
            _script.Clear();
            _cursor = 0;
            _order = 0;
            _sorted = true;
            for (int i = 0; i < _physical.Length; i++) _physical[i] = false;
        }

        void EnsureSorted()
        {
            if (_sorted) return;
            // 아직 안 쓴 줄만 (시각, 넣은 순서)로 안정 정렬한다.
            var rest = _script.GetRange(_cursor, _script.Count - _cursor);
            rest.Sort((a, b) => a.TimeU != b.TimeU ? a.TimeU.CompareTo(b.TimeU) : a.Order.CompareTo(b.Order));
            for (int i = 0; i < rest.Count; i++) _script[_cursor + i] = rest[i];
            _sorted = true;
        }

        public void Poll(long simTimeU, List<InputEvent> into)
        {
            EnsureSorted();
            while (_cursor < _script.Count && _script[_cursor].TimeU <= simTimeU)
            {
                var e = _script[_cursor++];
                _physical[(int)e.Key] = e.Down;
                long t = e.TimeU < simTimeU ? simTimeU : e.TimeU;
                _tracker.Apply(e.Key, e.Down, t, into);
            }
        }

        public bool IsDown(LogicalKey key) => _tracker.IsDown(key);

        /// <summary>시각이 된 줄을 가상 물리 상태에 조용히 반영한 뒤, 내부 상태와의 차이를 낸다(키보드의 Resync와 같다).</summary>
        public void Resync(long simTimeU, List<InputEvent> into, bool swallowHeldActions = false)
        {
            EnsureSorted();
            while (_cursor < _script.Count && _script[_cursor].TimeU <= simTimeU)
            {
                var e = _script[_cursor++];
                _physical[(int)e.Key] = e.Down;
            }
            var order = LogicalKeyTracker.EmitOrder;
            for (int n = 0; n < order.Length; n++)
                _tracker.Resync(order[n], _physical[(int)order[n]], simTimeU, into, swallowHeldActions);
        }

        public void ReleaseAll(long simTimeU, List<InputEvent> into) => _tracker.ReleaseAll(simTimeU, into);
    }
}
