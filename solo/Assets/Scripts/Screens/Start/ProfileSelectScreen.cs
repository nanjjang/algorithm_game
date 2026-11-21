using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 프로필 고르기(ScreenId.ProfileSelect, 08 3.3절, UA 4.3). 카드 2장(각 560×640, 간격 48): 오프라인(동작) / 서버(보이되 꺼짐,
    /// 이유 error.not_supported_offline — N-03 [임시]). 카드 그림 S-0301 496×240. 규칙은 ProfileSelectModel. Esc = 타이틀로.
    /// </summary>
    public sealed class ProfileSelectScreen : ScreenBase
    {
        const float CardW = 560f, CardH = 640f, CardGap = 48f, ArtW = 496f, ArtH = 240f, Pad = 32f;

        readonly UiButton[] _cards = new UiButton[ProfileSelectModel.CardCount];

        public override ScreenId Id => ScreenId.ProfileSelect;
        protected override string TitleKey => StartNameKeys.ProfileSelectTitle;

        protected override void BuildContent(RectTransform content)
        {
            float total = CardW * _cards.Length + CardGap * (_cards.Length - 1);
            float x0 = (UiScale.RefWidth - total) * 0.5f;
            float y = (ContentHeight - CardH) * 0.5f;
            for (int i = 0; i < _cards.Length; i++)
            {
                var kind = ProfileSelectModel.CardKinds[i];
                var b = UiKit.Button(Ctx, content, null, x0 + i * (CardW + CardGap), y, CardW, CardH, () => Pick(kind),
                    UiButtonStyle.Plain, Focus);
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.Panel(Ctx, root, UiPanelKind.Board, 0f, 0f, CardW, CardH);
                var art = Ctx != null && Ctx.Art != null ? Ctx.Art.Ui("S-0301", ProfileSelectModel.CardArtVariants[i]) : default(UiSprite);
                float artX = (CardW - ArtW) * 0.5f;
                if (art.IsValid) UiKit.Image(root, art.Sprite, artX, Pad, ArtW, ArtH, null, false);
                else UiKit.Panel(Ctx, root, UiPanelKind.Recess, artX, Pad, ArtW, ArtH);
                float ny = Pad + ArtH + 32f;
                float nh = UiTheme.LineHeight(UiFontId.F32);
                UiKit.Label(Ctx, root, T(ProfileSelectModel.CardNameKeys[i]), UiFontId.F32, Pad, ny, CardW - 2f * Pad, nh,
                    UiTheme.Chalk, TextAnchor.MiddleCenter);
                UiKit.Label(Ctx, root, T(ProfileSelectModel.CardDescKeys[i]), UiFontId.F16, Pad, ny + nh + 24f, CardW - 2f * Pad,
                    UiTheme.LineHeight(UiFontId.F16) * 4f, UiTheme.SlateBlue, TextAnchor.UpperCenter, wrap: true);
                string reason = ProfileSelectModel.CardDisabledReason(kind);
                if (reason != null) b.SetDisabled(reason);
                _cards[i] = b;
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Focus.SetCursor(0, false);
        }

        void Pick(ProfileKind kind)
        {
            ProfileSelectModel.Pick(kind, Ctx != null ? Ctx.Profile : null, Args).Apply(Screens, this);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}
