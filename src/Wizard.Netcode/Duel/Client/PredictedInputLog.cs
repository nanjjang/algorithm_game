using System;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // 예측 세계가 틱마다 실제로 넣은 입력(정렬된 열). 그 틱의 확정이 오면 이것과 견줘 예측이 맞았는지 본다(spec 10 14.7의 4).
    // 내 이벤트가 서버에서 버려진 경우도 "다름"이다(순번까지 같아야 같다).
    // 기록이 있는 틱은 [확정 틱, 예측 틱) 안이라 되감기 한도만큼이면 된다. 칸은 처음에 한 번 잡는다.
    internal sealed class PredictedInputLog
    {
        private readonly uint[] _ticks;
        private readonly bool[] _used;
        private readonly int[] _counts;
        private readonly PlayerInput[][] _inputs;

        public PredictedInputLog(int slots, int maxInputsPerTick)
        {
            if (slots < 1 || maxInputsPerTick < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slots));
            }
            _ticks = new uint[slots];
            _used = new bool[slots];
            _counts = new int[slots];
            _inputs = new PlayerInput[slots][];
            for (int i = 0; i < slots; i++)
            {
                _inputs[i] = new PlayerInput[maxInputsPerTick];
            }
        }

        public void Record(uint tick, ReadOnlySpan<PlayerInput> inputs)
        {
            int i = Find(tick);
            if (i < 0)
            {
                i = FreeSlot();
            }
            if (inputs.Length > _inputs[i].Length)
            {
                throw new InvalidOperationException("PredictedInputLog: too many inputs for one tick");
            }
            _ticks[i] = tick;
            _used[i] = true;
            _counts[i] = inputs.Length;
            inputs.CopyTo(_inputs[i]);
        }

        // 그 틱의 기록이 confirmed 와 같은가. 기록이 없으면 거짓(예측하지 않은 틱은 맞았다고 볼 수 없다)
        public bool Matches(uint tick, ReadOnlySpan<PlayerInput> confirmed)
        {
            int i = Find(tick);
            if (i < 0 || _counts[i] != confirmed.Length)
            {
                return false;
            }
            PlayerInput[] mine = _inputs[i];
            for (int k = 0; k < confirmed.Length; k++)
            {
                if (!Same(mine[k], confirmed[k]))
                {
                    return false;
                }
            }
            return true;
        }

        public void Forget(uint tick)
        {
            int i = Find(tick);
            if (i >= 0)
            {
                _used[i] = false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _used.Length; i++)
            {
                _used[i] = false;
            }
        }

        private static bool Same(in PlayerInput a, in PlayerInput b)
        {
            InputEvent x = a.Event, y = b.Event;
            return a.Slot == b.Slot && x.Tick == y.Tick && x.Offset == y.Offset && x.Key == y.Key && x.Down == y.Down && x.Seq == y.Seq;
        }

        private int Find(uint tick)
        {
            for (int i = 0; i < _ticks.Length; i++)
            {
                if (_used[i] && _ticks[i] == tick)
                {
                    return i;
                }
            }
            return -1;
        }

        // 빈 칸. 없으면 가장 이른 틱의 칸(한도를 넘는 기록은 쓰이지 않는다)
        private int FreeSlot()
        {
            int oldest = 0;
            for (int i = 0; i < _ticks.Length; i++)
            {
                if (!_used[i])
                {
                    return i;
                }
                if (_ticks[i] < _ticks[oldest])
                {
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}
