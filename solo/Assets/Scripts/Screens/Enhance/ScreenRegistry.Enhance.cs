namespace Wizard.Solo.UI
{
    /// <summary>강화 묶음 화면 등록(00 문서 2.1절·8절 표). 진입은 메인 메뉴와 정산 화면의 버튼뿐이다 — 판 중·야영에는 여는 길이 없다.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterEnhance(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.WandPicker, () => new Wizard.Solo.Screens.Enhance.WandPickerScreen());
            m.Register(ScreenId.Enhance, () => new Wizard.Solo.Screens.Enhance.EnhanceScreen());
            m.Register(ScreenId.WandReleaseConfirm, () => new Wizard.Solo.Screens.Enhance.WandReleaseConfirmScreen());
            m.Register(ScreenId.MutationRemove, () => new Wizard.Solo.Screens.Enhance.MutationRemoveScreen());
            m.Register(ScreenId.SlotEngrave, () => new Wizard.Solo.Screens.Enhance.SlotEngraveScreen());
            m.Register(ScreenId.EnhanceTutorial, () => new Wizard.Solo.Screens.Enhance.EnhanceTutorialScreen());
        }
    }
}
