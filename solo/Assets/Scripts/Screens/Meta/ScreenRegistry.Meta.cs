namespace Wizard.Solo.UI
{
    /// <summary>G24 Meta 묶음 등록(00 문서 2.1절·8절): Records, TitlePicker, Wardrobe, Shop, ShopBuyConfirm.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterMeta(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Records, () => new Wizard.Solo.Screens.Meta.RecordsScreen());
            m.Register(ScreenId.TitlePicker, () => new Wizard.Solo.Screens.Meta.TitlePickerScreen());
            m.Register(ScreenId.Wardrobe, () => new Wizard.Solo.Screens.Meta.WardrobeScreen());
            m.Register(ScreenId.Shop, () => new Wizard.Solo.Screens.Meta.ShopScreen());
            m.Register(ScreenId.ShopBuyConfirm, () => new Wizard.Solo.Screens.Meta.ShopBuyConfirmScreen());
        }
    }
}
