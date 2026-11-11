using Wizard.Sim.Input;

namespace Wizard.Client
{
    // CONTRACTS 6.5. 유니티 쪽이 만든다
    public readonly struct RawKeyEvent
    {
        public readonly LogicalKey Key;
        public readonly bool Down;
        public readonly double TimeSeconds;               // 하드웨어 이벤트 시각. 실수 시각은 여기까지만
        
        public RawKeyEvent(LogicalKey key, bool down, double timeSeconds)
        {
        Key = key;
        Down = down;
        TimeSeconds = timeSeconds;
        }
    }
}
