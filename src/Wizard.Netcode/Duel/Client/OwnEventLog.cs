using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // 내가 순번을 매겨 낸 이벤트의 열(CONTRACTS 6.4, 10.2). 순번 순이고, 시각((틱, 오프셋))도 줄지 않는다:
    // DuelClientCore 가 넣을 때 시각을 직전 이벤트보다 앞서지 않게 올리기 때문이다(한 패킷 안의 역행은 패킷 거부다, 10.6 (0)).
    // 그래서 같은 유효 틱의 이벤트는 열에서 이어 붙어 있고, 패킷의 겹쳐 보내는 창(ClientInputWindow)에 그대로 넘길 수 있다.
    //
    // 앞에서부터 지운다(Prune). 배열은 처음 크기로 잡고 모자라면 두 배로 늘린다(경기 중 서버가 오래 대답하지 않을 때만 늘어난다).
    internal sealed class OwnEventLog
    {
        private InputEvent[] _events;
        private OwnEventState[] _states;
        private int _start;
        private int _count;

        public OwnEventLog(int initialCapacity)
        {
            if (initialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }
            _events = new InputEvent[initialCapacity];
            _states = new OwnEventState[initialCapacity];
        }

        public int Count => _count;

        public ReadOnlySpan<InputEvent> Events => new ReadOnlySpan<InputEvent>(_events, _start, _count);

        public InputEvent At(int index) => _events[_start + index];

        public OwnEventState StateAt(int index) => _states[_start + index];

        public void SetState(int index, OwnEventState state) => _states[_start + index] = state;

        // 마지막 이벤트의 입력 시각. 열이 비었으면 −1
        public long LastTime => _count == 0 ? -1L : _events[_start + _count - 1].Time;

        public void Append(in InputEvent e)
        {
            if (_count > 0 && e.Time < LastTime)
            {
                throw new InvalidOperationException("OwnEventLog: time must not decrease");
            }
            EnsureRoom();
            _events[_start + _count] = e;
            _states[_start + _count] = OwnEventState.Pending;
            _count++;
        }

        // 유효 틱이 tick 인 이벤트의 범위 [first, first + length). 시각 순이라 뒤에서부터 찾는다(대개 끝 근처다)
        public void RangeAt(uint tick, out int first, out int length)
        {
            int end = _count;
            while (end > 0 && At(end - 1).Tick > tick)
            {
                end--;
            }
            int begin = end;
            while (begin > 0 && At(begin - 1).Tick == tick)
            {
                begin--;
            }
            first = begin;
            length = end - begin;
        }

        // 그 틱에서 버려지지 않은 내 이벤트 수(서버의 틱당 8개 검사와 같은 셈. 버려진 것은 서버도 세지 않는다)
        public int LiveCountAt(uint tick)
        {
            RangeAt(tick, out int first, out int length);
            int n = 0;
            for (int i = first; i < first + length; i++)
            {
                OwnEventState s = StateAt(i);
                if (s == OwnEventState.Pending || s == OwnEventState.Confirmed)
                {
                    n++;
                }
            }
            return n;
        }

        // 예측 세계에 넣을 그 틱의 내 이벤트(아직 확정되지 않았고 버려지지 않은 것)를 into 에 더한다. 더한 수를 돌려준다
        public int CollectPending(uint tick, PlayerSlot slot, Span<PlayerInput> into)
        {
            RangeAt(tick, out int first, out int length);
            int n = 0;
            for (int i = first; i < first + length; i++)
            {
                if (StateAt(i) == OwnEventState.Pending && n < into.Length)
                {
                    into[n++] = new PlayerInput(slot, At(i));
                }
            }
            return n;
        }

        // 앞에서부터 확정·버림이 끝났고 유효 틱이 keepFromTick 보다 이른 이벤트를 지운다(패킷의 창 밖, 예측에도 쓰지 않는다)
        public void Prune(uint keepFromTick)
        {
            while (_count > 0 && StateAt(0) != OwnEventState.Pending && At(0).Tick < keepFromTick)
            {
                _start++;
                _count--;
            }
            if (_count == 0)
            {
                _start = 0;
            }
        }

        private void EnsureRoom()
        {
            if (_start + _count < _events.Length)
            {
                return;
            }
            if (_start > 0 && _count < _events.Length / 2)
            {
                Array.Copy(_events, _start, _events, 0, _count);
                Array.Copy(_states, _start, _states, 0, _count);
                _start = 0;
                return;
            }
            var events = new InputEvent[_events.Length * 2];
            var states = new OwnEventState[_states.Length * 2];
            Array.Copy(_events, _start, events, 0, _count);
            Array.Copy(_states, _start, states, 0, _count);
            _events = events;
            _states = states;
            _start = 0;
        }
    }
}
