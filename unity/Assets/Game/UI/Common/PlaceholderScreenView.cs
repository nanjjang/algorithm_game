#nullable enable
using UnityEngine;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // 뷰가 아직 없는 화면의 자리 표시. 층에 맞는 판 하나와 화면 id 글자(이름 표 밖의 대체 표시, IAssetCatalog.Placeholder 와 같은 뜻)를 찍는다.
    // 누를 것이 없으므로 Esc(ScreenStack.Back)로만 나간다.
    //   Fullscreen  무대 전체를 나무 어두움으로 덮는다
    //   Panel       가운데 판. 그 판만 포인터를 막는다
    //   Modal       뒤를 어둡게 덮고 가운데 판
    //   Toast       위 가운데의 띠
    //   HudOverlay  바탕 없이 왼쪽 위에 글자만. 포인터를 막지 않는다
    public sealed class PlaceholderScreenView : UiScreenView
    {
        // 8px 격자에 놓이도록 고른 크기 [임시]
        public static readonly PixelRect CenterPanelRect = new PixelRect(480, 264, 960, 552);
        public static readonly PixelRect ToastRect = new PixelRect(720, UiLayout.SafeMarginPx, 480, 64);

        private PixelRect _cover;

        public PlaceholderScreenView(IScreen screen, UiViewContext context)
            : base(screen, context)
        {
        }

        public UiLabel? Title { get; private set; }

        public override PixelRect CoverRect => _cover;

        protected override void OnBuild()
        {
            PixelRect full = Root.LocalRect;
            UiWidget body;
            switch (Screen.Layer)
            {
                case ScreenLayer.Fullscreen:
                {
                    var bg = new UiPanel("Background", Root, full, framed: false);
                    bg.SetFill(UiPalette.WoodDark);
                    body = bg;
                    _cover = full;
                    break;
                }
                case ScreenLayer.Modal:
                {
                    var scrim = new UiPanel("Scrim", Root, full, framed: false);
                    scrim.SetFill(UiPalette.Scrim);
                    body = new UiPanel("Panel", Root, CenterPanelRect);
                    _cover = full;
                    break;
                }
                case ScreenLayer.Panel:
                    body = new UiPanel("Panel", Root, CenterPanelRect);
                    _cover = CenterPanelRect;
                    break;
                case ScreenLayer.Toast:
                    body = new UiPanel("Toast", Root, ToastRect);
                    _cover = default;
                    break;
                default:
                    body = new UiWidget("Hud", Root, full);
                    _cover = default;
                    break;
            }
            int margin = Screen.Layer == ScreenLayer.Fullscreen || Screen.Layer == ScreenLayer.HudOverlay ? UiLayout.SafeMarginPx : UiLayout.GridPx * 2;
            int width = body.LocalRect.Width - margin - margin;
            Title = new UiLabel(Context, body, new PixelRect(margin, margin / 2, width, UiFonts.F32.LineHeightPx), UiFontId.F32, TextAnchor.MiddleLeft);
            Title.SetRaw(Screen.Id.ToString());
        }
    }
}
