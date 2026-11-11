using Wizard.Core;

namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1
    public readonly struct RunCommand
    {
        public readonly uint Tick;          // 이 틱의 키 이벤트보다 먼저 적용
        public readonly uint Seq;           // 키 이벤트와 같은 순번 공간
        public readonly PlayerSlot Slot;
        public readonly RunCommandOp Op;
        public readonly int A, B;

        public RunCommand(uint tick, uint seq, PlayerSlot slot, RunCommandOp op, int a, int b)
        {
            Tick = tick;
            Seq = seq;
            Slot = slot;
            Op = op;
            A = a;
            B = b;
        }

        public bool IsDebug => (int)Op >= 200;
    }
}
