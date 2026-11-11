namespace Wizard.Sim.Input
{
    // CONTRACTS 3.8. 순번이 없는 RunCommand(op, a, b). InputRecorder 가 순번을 매긴다
    public readonly struct RunCommandDraft
    {
        public readonly RunCommandOp Op;
        public readonly int A, B;

        public RunCommandDraft(RunCommandOp op, int a, int b)
        {
            Op = op;
            A = a;
            B = b;
        }
    }
}
