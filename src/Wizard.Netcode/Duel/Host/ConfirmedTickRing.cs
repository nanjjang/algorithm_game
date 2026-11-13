using System;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // 최근 확정 틱의 입력(ServerTick 이 ack 받지 못한 틱부터 다시 싣는다, CONTRACTS 10.3). 틱마다 슬롯별로 확정 순서(InputOrder 순)대로 둔다.
    // 마지막 RingTicks 개만 남는다. 그보다 뒤처진 클라이언트는 남은 가장 오래된 틱부터 받고, 확정 공백으로 전체 상태를 청한다(6.4절).
    internal sealed class ConfirmedTickRing
    {
        public const int RingTicks = 256;                       // 자료구조 크기
        private const int PerSlot = InputOrder.MaxEventsPerTickPerPlayer;
        private const int Slots = DuelWire.DuelSlots;

        private readonly InputEvent[] _events = new InputEvent[RingTicks * Slots * PerSlot];
        private readonly byte[] _counts = new byte[RingTicks * Slots];
        private uint _end;                                      // 다음에 넣을 틱(= 확정한 틱 수)

        public uint EndTick => _end;

        public uint OldestTick => _end > RingTicks ? _end - RingTicks : 0U;

        // 틱 tick(= EndTick)의 확정 입력. sorted 는 그 틱에 Step 에 넘긴 열(InputOrder 순)
        public void Store(uint tick, ReadOnlySpan<PlayerInput> sorted)
        {
            if (tick != _end)
            {
                throw new InvalidOperationException("ConfirmedTickRing: ticks must be stored in order");
            }
            int row = (int)(tick % RingTicks);
            for (int s = 0; s < Slots; s++)
            {
                _counts[row * Slots + s] = 0;
            }
            for (int i = 0; i < sorted.Length; i++)
            {
                int s = sorted[i].Slot.Value;
                int cell = row * Slots + s;
                int n = _counts[cell];
                _events[cell * PerSlot + n] = sorted[i].Event;
                _counts[cell] = (byte)(n + 1);
            }
            _end = tick + 1;
        }

        // 남아 있는 틱(OldestTick ≤ tick < EndTick)의 한 슬롯 입력
        public ReadOnlySpan<InputEvent> Events(uint tick, int slot)
        {
            if (tick >= _end || tick < OldestTick || (uint)slot >= Slots)
            {
                return ReadOnlySpan<InputEvent>.Empty;
            }
            int cell = (int)(tick % RingTicks) * Slots + slot;
            return new ReadOnlySpan<InputEvent>(_events, cell * PerSlot, _counts[cell]);
        }
    }
}
