namespace Wizard.Sim.Duel
{
    // CONTRACTS 3.9. 이탈·기권·몰수·무효는 코어 밖(호스트)
    public enum DuelEndReason : byte
    {
        Ko = 0,
        Timeout = 1,
    }
}
