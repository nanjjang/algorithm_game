namespace Wizard.Solo.Meta
{
    /// <summary>G13 골드 상점 등록(00 문서 2.1절).</summary>
    public static partial class MetaFactory
    {
        static partial void CreateShop(GameContext ctx, ref IShopService s)
        {
            s = new Shop.ShopService(ctx);
        }
    }
}
