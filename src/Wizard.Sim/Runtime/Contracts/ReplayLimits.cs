namespace Wizard.Sim
{
    // CONTRACTS 3.8. 값은 SimLimitsDef(data/sim/limits)
    public readonly struct ReplayLimits
    {
        public readonly uint MaxTicks;
        public readonly long MaxStepCount;
        
        public ReplayLimits(uint maxTicks, long maxStepCount)
        {
        MaxTicks = maxTicks;
        MaxStepCount = maxStepCount;
        }
    }
}
