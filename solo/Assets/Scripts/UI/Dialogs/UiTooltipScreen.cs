using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 툴팁(ScreenId.Tooltip, Toast 층 — Top·입력에 세지 않는다). 최대 폭 400, K-08 판, 16px 글(여러 줄).
    /// 인자 Text(이미 이름 표를 거친 글). ScreenManager.ShowTooltip(글, 칸)이 칸의 오른쪽 아래(넘치면 왼쪽·위)에 놓는다.
    /// 물건 툴팁의 줄 구성(이름 등급색, 종류·속성, 수치, 설명)은 그 화면이 글을 만들어 넘긴다.
    /// </summary>
    public sealed class UiTooltipScreen : ScreenBase
    {
        public const float MaxWidth = 400f, Pad = 12f, OffsetPx = 8f;
        RectTransform _box;
        UImage _panel;
        UiLabel _text;

        public override ScreenId Id => ScreenId.Tooltip;
        public override ScreenLayer Layer => ScreenLayer.Toast;
        public override bool BlocksInput => false;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _panel = UiKit.Panel(Ctx, content, UiPanelKind.Tooltip, 0f, 0f, MaxWidth, 48f);
            _panel.raycastTarget = false;
            _box = _panel.rectTransform;
            _box.name = "Tooltip";
            _text = UiKit.Label(Ctx, _box, string.Empty, UiFontId.F16, Pad, Pad, MaxWidth - 2f * Pad, UiTheme.LineHeight(UiFontId.F16),
                UiTheme.Chalk, TextAnchor.UpperLeft, wrap: true);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Show(Args.Get(ScreenArgKeys.Text, string.Empty), null);
        }

        /// <summary>글과 자리를 정한다. anchor가 null이면 화면 가운데 위.</summary>
        public void Show(string text, RectTransform anchor)
        {
            if (_text == null) return;
            _text.SetText(text ?? string.Empty);
            int fontPx = (int)UiFontId.F16;
            float textW = Mathf.Min(MaxWidth - 2f * Pad, UiTextFit.WidthPx(text, fontPx) + 2f);
            float lineH = UiTheme.LineHeight(UiFontId.F16);
            int lines = Mathf.Max(1, CountLines(text, fontPx, (int)(MaxWidth - 2f * Pad)));
            float w = textW + 2f * Pad, h = lines * lineH + 2f * Pad;
            UiKit.Place(_text.Rect, Pad, Pad, textW, lines * lineH);
            Vector2 pos = new Vector2((UiScale.RefWidth - w) * 0.5f, 96f);
            if (anchor != null && Root != null)
            {
                var corners = new Vector3[4];
                anchor.GetWorldCorners(corners);
                // Root 기준(왼쪽 위 원점, y 아래 +)으로 바꾼다.
                Vector2 br = ToTopLeft(corners[3]);
                Vector2 tl = ToTopLeft(corners[1]);
                pos = new Vector2(br.x + OffsetPx, br.y + OffsetPx);
                if (pos.x + w > UiScale.RefWidth) pos.x = Mathf.Max(0f, tl.x - OffsetPx - w);
                if (pos.y + h > UiScale.RefHeight) pos.y = Mathf.Max(0f, tl.y - OffsetPx - h);
            }
            UiKit.Place(_box, pos.x, pos.y, w, h);
        }

        Vector2 ToTopLeft(Vector3 world)
        {
            Vector3 local = Root.InverseTransformPoint(world);
            var r = Root.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        /// <summary>폭 안에서 몇 줄이 되는가(UiTextFit 폭 규칙, 줄바꿈 글자 포함).</summary>
        public static int CountLines(string text, int fontPx, int maxWidthPx)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int lines = 0;
            var parts = text.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                int w = UiTextFit.WidthPx(parts[i], fontPx);
                lines += Mathf.Max(1, Mathf.CeilToInt(w / (float)Mathf.Max(1, maxWidthPx)));
            }
            return lines;
        }

        public override bool OnBack() => false;
        public override bool OnMenuKey(MenuKey key) => false;
    }
}
