using System;
using Wizard.Content;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. 정규 바이트 = .wzrc 머리말 그대로(11.1절). 서버 프로필에서는 서버가 만든 바이트를 그대로 쓴다.
    // WriteCanonical/TryReadCanonical 은 M1-SIM-07 이 더한다
    public sealed class EndlessSetup
    {
        public Guid RunId;
        public ulong Seed;
        public RunKind Kind;
        public int StartWave = 1;                         // 1, 11, 21, …
        public bool PhantomFirst;
        public WandSnapshot Wand = new WandSnapshot();
        public ArtMask UnlockedArts;
        public bool SeasonTagOn;                          // 시즌 주화·한정 드랍 기회를 기록할지
    }
}
