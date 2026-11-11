namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4. 소리와 피격 연출은 Confirmed 에서만 낸다
    public enum EventPhase : byte
    {
        Predicted = 0,
        Confirmed = 1,
        Retracted = 2,
    }
}
