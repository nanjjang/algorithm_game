namespace Wizard.Solo.UI
{
    /// <summary>정산 묶음 등록(00 문서 2.1·8절): Settlement(기록, 귀환 가방, 가져가는 것).</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterSettlement(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Settlement, () => new Screens.Settlement.SettlementScreen());
        }
    }
}
