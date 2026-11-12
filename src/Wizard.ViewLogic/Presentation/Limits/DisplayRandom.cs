namespace Wizard.ViewLogic.Presentation.Limits
{
    // 표시 전용 난수(흔들림 방향, 입자 흩뿌림). 코어 난수기(Pcg32, RngStreams)를 쓰지 않는다:
    // 쓰면 표시가 세계의 난수 흐름을 소비해 재실행 결과가 달라진다(ARCHITECTURE 9절). 결과가 재현될 필요도 없다.
    // xorshift32 (Marsaglia). 씨앗 0 은 고정점이라 1 로 바꾼다.
    public sealed class DisplayRandom
    {
        private const float UnitScale = 1f / 16777216f;   // 2^-24: 위 24비트를 [0, 1) 실수로(단위 환산 상수)

        private uint _state;

        public DisplayRandom(uint seed)
        {
            _state = seed == 0u ? 1u : seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        // [0, 1)
        public float NextUnit() => (NextUInt() >> 8) * UnitScale;

        // [−1, 1)
        public float NextSigned() => NextUnit() * 2f - 1f;
    }
}
