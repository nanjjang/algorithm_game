using Wizard.Sim.Arts;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 시험용 결정 지갑(spec 03 9.1). 실제로는 PlayerState 가 구현한다
    internal sealed class TestWallet : ICrystalWallet
    {
        public TestWallet(int cap, int crystals, int debt, bool hasCredit)
        {
            Cap = cap;
            Crystals = crystals;
            Debt = debt;
            HasCredit = hasCredit;
        }

        public int Crystals { get; set; }

        public int Cap { get; }

        public int Debt { get; set; }

        public bool HasCredit { get; }

        public void CopyFrom(TestWallet other)
        {
            Crystals = other.Crystals;
            Debt = other.Debt;
        }
    }
}
