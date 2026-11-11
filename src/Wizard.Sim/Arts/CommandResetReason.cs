namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5
    public enum CommandResetReason : byte
    {
        StanceEnter,
        StanceExit,
        Reverse,
        Pause,
        WaveStart,
        RoundStart,
        Fired,
        Fizzled,
        Timeout,
    }
}
