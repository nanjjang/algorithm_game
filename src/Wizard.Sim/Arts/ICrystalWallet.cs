namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5. PlayerState 가 구현한다
    public interface ICrystalWallet
    {
        int Crystals { get; set; }
        int Cap { get; }
        int Debt { get; set; }
        bool HasCredit { get; }
    }
}
