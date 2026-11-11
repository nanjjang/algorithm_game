namespace Wizard.Rules.Economy
{
    // CONTRACTS 4.3. 거절 사유는 NeedWave10, NeedPlay2h 등 spec 11 6절의 값이다
    public sealed class TradeEligibility
    {
        public int MaxWaveReached;
        public long PlaySeconds;
        public bool OnHold;
        public bool HasDebt;
    }
}
