namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1. 순번이 아직 없는 키 이벤트. InputQuantizer 가 낸다(6.5절)
    public readonly struct QuantizedKey
    {
        public readonly uint Tick;
        public readonly byte Offset;
        public readonly LogicalKey Key;
        public readonly bool Down;

        public QuantizedKey(uint tick, byte offset, LogicalKey key, bool down)
        {
            Tick = tick;
            Offset = offset;
            Key = key;
            Down = down;
        }

        public long Time => (long)Tick * 256 + Offset;
    }
}
