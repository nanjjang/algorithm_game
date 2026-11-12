namespace Wizard.ReplayCli
{
    // .wzrc 의 0x04 중간 지점 레코드 하나(CONTRACTS 11.1). 해시는 world.Tick == Tick 일 때(그 틱의 Step 전)의 상태 해시다
    public readonly struct RecordMark
    {
        public RecordMark(int wave, uint tick, ulong stateHash)
        {
            Wave = wave;
            Tick = tick;
            StateHash = stateHash;
        }

        public int Wave { get; }

        public uint Tick { get; }

        public ulong StateHash { get; }
    }
}
