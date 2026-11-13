namespace Wizard.Netcode.Tests.NetSim
{
    // NetSim 의 난수(SplitMix64). 런타임의 System.Random 구현에 기대지 않고 씨앗 하나로 같은 열이 나온다.
    // 링크마다 따로 갈라 쓴다(Derive): 한 링크의 조건을 바꿔도 다른 링크의 열은 그대로다
    internal sealed class NetRng
    {
        private const int PpmScale = 1000000;   // 확률 단위(백만분율)
        private ulong _state;

        public NetRng(ulong seed)
        {
            _state = seed;
        }

        public static ulong Derive(ulong seed, ulong stream, ulong sub)
        {
            var r = new NetRng(seed ^ (stream * 0x9E3779B97F4A7C15UL) ^ (sub * 0xC2B2AE3D27D4EB4FUL));
            r.Next();
            return r.Next();
        }

        public ulong Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // [0, n). n ≤ 0 이면 0
        public long Below(long n) => n <= 0 ? 0L : (long)(Next() % (ulong)n);

        // [0, n] 균등
        public long UpTo(long n) => Below(n + 1);

        // 백만분율 ppm 의 확률로 참. 호출마다 난수 하나를 쓴다(조건과 무관하게 열이 같은 자리에서 나아가게)
        public bool Ppm(int ppm)
        {
            long r = Below(PpmScale);
            return r < ppm;
        }
    }
}
