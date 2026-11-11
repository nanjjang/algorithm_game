namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3
    public readonly struct RecentInputs
    {
        public readonly int Count;                        // 최대 5개. 0번이 가장 최근. Dir(i)·Chained(i) 는 M1-SIM-08 이 더한다
    }
}
