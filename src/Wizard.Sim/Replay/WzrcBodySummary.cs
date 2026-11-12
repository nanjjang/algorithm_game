namespace Wizard.Sim
{
    // InputRecordCodec 이 .wzrc 본문을 한 번 훑으며 센 것(InputRecordBody 의 칸이 된다)
    internal struct WzrcBodySummary
    {
        public int CommandCount;
        public int InputCount;
        public int PauseCount;
        public int CheckpointCount;
        public long PauseTotalMs;
        public bool HasDebugOps;
    }
}
