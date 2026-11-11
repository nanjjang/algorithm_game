using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Waves
{
    // CONTRACTS 3.7
    public interface IWaveDirector
    {
        WavePlan Plan(ulong seed, int wave, FrameId previousFrame, bool phantomFirst);    // 순수 함수
        void Begin(WavePlan plan);
        void Tick(uint tick, IEnemySpawner spawner, ref Pcg32 spawnRng);                  // 묶음 방출, 지체 방지
        bool AllReleased { get; }
    }
}
