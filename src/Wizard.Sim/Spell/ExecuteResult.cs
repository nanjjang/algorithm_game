namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4
    public readonly struct ExecuteResult
    {
        public readonly int HeatToAddHu;
        public readonly int Spawned, Omitted;
        public readonly bool SelfDetonated;
    }
}
