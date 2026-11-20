namespace Wizard.Solo.Meta
{
    public static class MetaFactory
    {
        public static void Fill(GameContext ctx)
        {
            ctx.Enhance = new Enhance.EnhanceService(ctx);
            ctx.Wands = new Inventory.WandService(ctx);
            ctx.Shop = new Shop.ShopService(ctx);
            ctx.Settlement = new Settlement.SettlementService(ctx);
        }
    }
}
