namespace Wizard.Core
{
    // CONTRACTS 1.3, spec 07 14.2. 용도·웨이브별 난수는 판 씨앗에서 파생한다.
    // "세계가 보관"인 흐름은 웨이브(결투는 라운드) 시작 때 다시 파생한다. 앞 웨이브의 소비가 뒤로 번지지 않는다.
    public static class RngStreams
    {
        public static ulong SplitMix64(ulong x)
        {
            ulong z = unchecked(x + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        // Pcg32(SplitMix64(seed ^ ((ulong)stream << 48) ^ ((ulong)(uint)wave << 16) ^ (ushort)sub), seq: (ulong)stream)
        public static Pcg32 Derive(ulong runSeed, RngStream stream, int wave, int sub = 0)
        {
            ulong mixed = runSeed ^ ((ulong)stream << 48) ^ ((ulong)(uint)wave << 16) ^ (ushort)sub;
            return new Pcg32(SplitMix64(mixed), (ulong)stream);
        }
    }
}
