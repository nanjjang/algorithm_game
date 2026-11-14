namespace Wizard.Solo.Meta
{
    /// <summary>G13 지팡이·서고 서비스 등록(00 문서 2.1절).</summary>
    public static partial class MetaFactory
    {
        static partial void CreateWands(GameContext ctx, ref IWandService s)
        {
            s = new Inventory.WandService(ctx);
        }
    }
}
