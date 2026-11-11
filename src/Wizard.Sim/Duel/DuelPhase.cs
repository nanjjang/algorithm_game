namespace Wizard.Sim.Duel
{
    // CONTRACTS 3.9
    public enum DuelPhase : byte
    {
        Countdown = 0,
        Live = 1,
        Overtime = 2,
        RoundResult = 3,
        Intermission = 4,
        MatchOver = 5,
    }
}
