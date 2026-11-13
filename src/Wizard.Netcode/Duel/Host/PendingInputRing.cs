using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // 한 사람의, 받아들였지만 아직 확정하지 않은 이벤트(CONTRACTS 10.6 Accept). 유효 틱마다 8칸, 틱 RingTicks 개를 고리로 둔다.
    // 틱 t 의 칸은 t mod RingTicks 이다. 든 틱은 늘 [확정할 다음 틱, + RingTicks) 안이다: 그보다 앞선 틱은 봉인돼 늦음으로 버려지고
    // (확정은 양쪽 봉인 뒤에만 일어난다), 뒤의 틱은 Fits 가 거른다. 확정한 틱은 Take 가 비운다. 할당하지 않는다.
    internal sealed class PendingInputRing
    {
        public const int RingTicks = 256;                       // 자료구조 크기(4.3초). 이른 시각 검사를 지난 이벤트는 지금 틱 + D + 1 안이다
        private const int PerTick = InputOrder.MaxEventsPerTickPerPlayer;

        private readonly InputEvent[] _events = new InputEvent[RingTicks * PerTick];
        private readonly uint[] _tags = new uint[RingTicks];
        private readonly byte[] _counts = new byte[RingTicks];

        public static bool Fits(uint tick, uint nextConfirmTick) => tick >= nextConfirmTick && tick - nextConfirmTick < RingTicks;

        public int Count(uint tick)
        {
            int i = (int)(tick % RingTicks);
            return _counts[i] > 0 && _tags[i] == tick ? _counts[i] : 0;
        }

        // 전제: Fits, Count(tick) < 8. 받은 순서(= 순번 순)로 쌓는다
        public void Add(in InputEvent e)
        {
            int i = (int)(e.Tick % RingTicks);
            int n = Count(e.Tick);
            _tags[i] = e.Tick;
            _events[i * PerTick + n] = e;
            _counts[i] = (byte)(n + 1);
        }

        // 틱 tick 의 이벤트를 dest 에 (slot, 이벤트)로 옮기고 칸을 비운다. 옮긴 수를 돌려준다
        public int Take(uint tick, PlayerSlot slot, Span<PlayerInput> dest)
        {
            int n = Count(tick);
            int i = (int)(tick % RingTicks);
            for (int k = 0; k < n; k++)
            {
                dest[k] = new PlayerInput(slot, _events[i * PerTick + k]);
            }
            _counts[i] = 0;
            return n;
        }
    }
}
