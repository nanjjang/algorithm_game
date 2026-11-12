using Wizard.Sim.Input;

namespace Wizard.Perf
{
    // 각본의 키 이벤트 하나(순번 없음). 순번은 StepFeed 와 InputRecorder 가 매긴다(CONTRACTS 3.1: 정렬한 뒤 순번)
    internal readonly struct ScriptKey
    {
        public readonly uint Tick;
        public readonly byte Slot;
        public readonly byte Offset;
        public readonly LogicalKey Key;
        public readonly bool Down;

        public ScriptKey(uint tick, byte slot, byte offset, LogicalKey key, bool down)
        {
            Tick = tick;
            Slot = slot;
            Offset = offset;
            Key = key;
            Down = down;
        }
    }
}
