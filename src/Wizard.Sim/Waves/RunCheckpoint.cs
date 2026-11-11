namespace Wizard.Sim.Waves
{
    // CONTRACTS 3.7. 5웨이브 지점: (wave, tick, state_hash). 서버가 받아 둔 값은 구속력이 있다(8.3절)
    public readonly struct RunCheckpoint
    {
        public readonly int Wave;
        public readonly uint Tick;
        public readonly ulong StateHash;
        
        public RunCheckpoint(int wave, uint tick, ulong stateHash)
        {
        Wave = wave;
        Tick = tick;
        StateHash = stateHash;
        }
    }
}
