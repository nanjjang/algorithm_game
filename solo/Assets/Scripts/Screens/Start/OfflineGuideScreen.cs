using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 오프라인 안내(ScreenId.OfflineGuide, 08 3.4절, UA 4.4). 판 800×560: 제목, 세 줄, 되는 것·안 되는 것 표(736×240, 두 열),
    /// [확인](256×64). 되는 것은 찬 동그라미, 안 되는 것은 흐리게 하지 않고 빈 동그라미. 규칙은 OfflineGuideModel.
    /// Esc·K = 들어온 곳으로(시작 흐름이면 타이틀·프로필 고르기, 다시 보기면 메인 메뉴).
    /// </summary>
    public sealed class OfflineGuideScreen : ScreenBase
    {
        const float BoardW = 800f, BoardH = 560f, Pad = 32f, TableW = 736f, TableH = 240f, BtnW = 256f, BtnH = 64f;
        const int DotRadius = 6;
        const float RowH = 26f;

        public override ScreenId Id => ScreenId.OfflineGuide;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            board.name = "OfflineGuideBoard";

            float y = Pad - 8f;
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, board, T(StartNameKeys.OfflineGuideTitle), UiFontId.F32, Pad, y, BoardW - 2f * Pad, th, UiTheme.Chalk,
                TextAnchor.MiddleCenter);
            y += th + 8f;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            for (int i = 0; i < OfflineGuideModel.LineKeys.Length; i++)
            {
                UiKit.Label(Ctx, board, T(OfflineGuideModel.LineKeys[i]), UiFontId.F16, Pad, y, BoardW - 2f * Pad, lh, UiTheme.Chalk);
                y += lh;
            }
            y += 16f;

            float tx = (BoardW - TableW) * 0.5f;
            var table = UiKit.Panel(Ctx, board, UiPanelKind.Recess, tx, y, TableW, TableH).rectTransform;
            table.name = "CanCannotTable";
            float colW = TableW * 0.5f;
            Column(table, 0f, colW, StartNameKeys.OfflineGuideColumnCan, OfflineGuideModel.CanKeys, false);
            Column(table, colW, colW, StartNameKeys.OfflineGuideColumnCannot, OfflineGuideModel.CannotKeys, true);
            UiKit.Divider(Ctx, table, colW - 2f, 8f, TableH - 16f, true);

            UiKit.Button(Ctx, board, T(StartNameKeys.CommonOk), (BoardW - BtnW) * 0.5f, BoardH - Pad - BtnH, BtnW, BtnH, Confirm,
                UiButtonStyle.Large, Focus);
        }

        void Column(RectTransform table, float x, float w, string headKey, string[] keys, bool hollow)
        {
            const float In = 16f;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, table, T(headKey), UiFontId.F16, x + In, 8f, w - 2f * In, lh, UiTheme.SlateBlue);
            float y = 8f + lh + 8f;
            Sprite dot = Ctx != null && Ctx.Art != null ? Ctx.Art.Circle(DotRadius, hollow) : null;
            for (int i = 0; i < keys.Length; i++)
            {
                float d = DotRadius * 2f;
                if (dot != null) UiKit.Image(table, dot, x + In, y + (RowH - d) * 0.5f, d, d, UiTheme.Chalk, false);
                UiKit.Label(Ctx, table, T(keys[i]), UiFontId.F16, x + In + d + 12f, y, w - 2f * In - d - 12f, RowH, UiTheme.Chalk);
                y += RowH;
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Focus.SetCursor(0, false);
        }

        void Confirm()
        {
            OfflineGuideModel.Confirm(Ctx != null ? Ctx.Profiles : null, Args).Apply(Screens, this);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}
