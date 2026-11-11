using System;
using Wizard.Content;

namespace Wizard.Sim.Waves
{
    // CONTRACTS 3.7. 판이 끝났을 때의 결과. WriteFacts(RunResultFacts 정규 바이트)는 M6-SIM-01 이 더한다.
    // 불변식: 정산에 쓰이는 모든 필드는 facts 바이트 안에 있다
    public sealed class RunOutcome
    {
        public RunEndKind EndKind;
        public uint EndTick;
        public ulong FinalStateHash;
        public int StartWave, ReachedWave, ClearedWave;
        public long Score;
        public WaveResult[] Waves = Array.Empty<WaveResult>();
        public GoldBreakdown Gold;
        public DropChance[] DropChances = Array.Empty<DropChance>();
        public WildBlock[] WildBlocksFinal = Array.Empty<WildBlock>();     // 귀환 가방 후보
        public PhantomResult Phantom;
        public ArtId[] ArtsUnlockedThisRun = Array.Empty<ArtId>();
        public RunCheckpoint[] Checkpoints = Array.Empty<RunCheckpoint>();
        public bool DebugUsed;                                            // 디버그 op 이 한 번이라도 적용됐는가
    }
}
