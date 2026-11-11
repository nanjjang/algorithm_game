using System;
using Wizard.Content;

namespace Wizard.Sim.Waves
{
    // CONTRACTS 3.7. (씨앗, 웨이브, 직전 틀)에서 정해지는 것
    public sealed class WavePlan
    {
        public int Wave;
        public TimeOfDay Tod;
        public FrameId Frame;
        public ModifierId Modifier1, Modifier2;
        public BossId Boss;
        public bool PhantomFirstEncounter;
        public SpawnBundle[] Bundles = Array.Empty<SpawnBundle>();
        public int EliteCount;
        public int Budget;
    }
}
