using Wizard.Solo.Art;
using System;
using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 메뉴 화면 공용 배치 규칙(가독성 개편). 모든 화면이 같은 격자·글자 크기·색·버튼 자리를 쓰게 한다.
    ///
    /// 격자: 1920 폭, 바깥 여백 64, 12열, 열 사이 24 → 열 폭 (1920 - 128 - 11*24) / 12 = 127.33.
    ///   ColX(col) = col번째 열의 왼쪽 x(0부터), ColW(span) = span열 폭(사이 간격 포함). 세로 리듬 8px(Snap, V(n)).
    /// 글자: 화면 글은 최소 F16(F12를 내용에 쓰지 않는다). 이름표·설명 = F16 회청(SlateBlue), 본문 = F24(자리가 되면),
    ///   구역 제목 = F32, 큰 숫자·제목 = F48/F64. 한 구역에 크기는 세 가지까지.
    /// 색: 본문 Chalk, 이름표만 SlateBlue(값에는 쓰지 않는다), 값·숫자 Chalk(재화는 Gold), 구역 선 Brass. 위험색(적 전용) 금지.
    /// 판: 금 모서리 장식 판 대신 평평한 면(Surface). level 0 = 들어간 면(어두움), 1 = 올라온 면. 모서리는 2px 깎아 둥근 느낌.
    /// 주 버튼: 화면마다 하나, 항상 오른쪽 아래 같은 자리(PrimaryRect, 416×88, 초록 Good 바탕 + 먹색 F32 글).
    ///   보조 버튼: 조용한 꼴(투명·들어간 바탕, Chalk F24).
    /// 좌표는 UiKit과 같다: 부모의 왼쪽 위가 원점, y 아래로 +. 글은 부르는 쪽이 이름 표를 거쳐 넘긴다.
    /// </summary>
    public static class UiLayout
    {
        public const float ScreenW = 1920f, ScreenH = 1080f;
        public const float Outer = 64f, ColGap = 24f;
        public const int Columns = 12;
        public const float ColUnit = (ScreenW - 2f * Outer - (Columns - 1) * ColGap) / Columns;
        /// <summary>세로 리듬 단위.</summary>
        public const float Rhythm = 8f;

        public const float PrimaryW = 416f, PrimaryH = 88f, PrimaryMargin = 24f;
        public const float SecondaryH = 64f;
        public const float SectionH = 40f;
        public const float RowH = 32f;
        public const float ChipH = 32f;
        public const float SurfacePad = 24f;

        public const float SurfaceAlpha = 0.85f;
        const float Corner = 2f;

        // ------------------------------------------------------------------ 격자

        /// <summary>col번째 열(0..11)의 왼쪽 x(화면 기준).</summary>
        public static float ColX(int col) => Outer + Mathf.Clamp(col, 0, Columns) * (ColUnit + ColGap);

        /// <summary>span열의 폭(열 사이 간격 포함).</summary>
        public static float ColW(int span)
        {
            span = Mathf.Clamp(span, 1, Columns);
            return span * ColUnit + (span - 1) * ColGap;
        }

        /// <summary>리듬 n칸(8px × n).</summary>
        public static float V(int n) => n * Rhythm;

        /// <summary>8px 배수로 올림.</summary>
        public static float Snap(float y) => Mathf.Ceil(y / Rhythm) * Rhythm;

        /// <summary>안쪽 내용 폭(1920 - 64*2).</summary>
        public static float InnerW => ScreenW - 2f * Outer;

        // ------------------------------------------------------------------ 면

        /// <summary>평평한 면. level 0 = 들어간(WoodDark), 1 이상 = 올라온(Wood, 위 1px 빛줄). 반환 = 면 노드(자식을 여기에).</summary>
        public static RectTransform Surface(GameContext ctx, RectTransform parent, float x, float y, float w, float h, int level = 1)
        {
            var node = UiKit.Node(parent, level <= 0 ? "Surface.Recessed" : "Surface.Raised", x, y, w, h);
            Color fill = UiTheme.WithAlpha(level <= 0 ? UiTheme.WoodDark : UiTheme.Wood, SurfaceAlpha);
            FillRounded(node, w, h, fill);
            if (level > 0 && w > 2f * Corner) UiKit.Rect(node, Corner, 0f, w - 2f * Corner, 1f, UiTheme.Sheen, false, "Sheen");
            return node;
        }

        /// <summary>모서리를 깎은 채움(가로 띠 + 세로 띠 두 장). raycast는 끈다.</summary>
        static void FillRounded(RectTransform parent, float w, float h, Color c)
        {
            if (w <= 2f * Corner || h <= 2f * Corner) { UiKit.Rect(parent, 0f, 0f, w, h, c, false, "Fill"); return; }
            UiKit.Rect(parent, Corner, 0f, w - 2f * Corner, h, c, false, "Fill");
            UiKit.Rect(parent, 0f, Corner, Corner, h - 2f * Corner, c, false, "FillL");
            UiKit.Rect(parent, w - Corner, Corner, Corner, h - 2f * Corner, c, false, "FillR");
        }

        /// <summary>구역 이름(Brass) + 아래 1px Brass 선(알파 0.5). title은 이름 표를 거친 글. 반환 = 그 아래 y(+8).</summary>
        public static float Section(GameContext ctx, RectTransform parent, float x, float y, float w, string title, UiFontId font = UiFontId.F16)
        {
            int lh = UiTheme.LineHeight(font);
            UiKit.Label(ctx, parent, title ?? string.Empty, font, x, y, w, lh + 8f, UiTheme.Brass, TextAnchor.MiddleLeft);
            UiKit.Rect(parent, x, y + Mathf.Max(32f, lh + 4f), w, 1f, UiTheme.WithAlpha(UiTheme.Brass, 0.5f), false, "SectionRule");
            return y + SectionH;
        }

        /// <summary>한 줄(32px): 왼쪽 회청 F16 이름표, 오른쪽 F24 값. valueColor 없으면 Chalk. 반환 = 값 글자 칸.</summary>
        public static UiLabel StatRow(GameContext ctx, RectTransform parent, float x, float y, float w, string label, string value, Color? valueColor = null)
        {
            float half = w * 0.5f;
            UiKit.Label(ctx, parent, label ?? string.Empty, UiFontId.F16, x, y, half, RowH, UiTheme.SlateBlue, TextAnchor.MiddleLeft);
            return UiKit.Label(ctx, parent, value ?? string.Empty, UiFontId.F24, x + half, y, w - half, RowH,
                valueColor ?? UiTheme.Chalk, TextAnchor.MiddleRight);
        }

        /// <summary>작은 알약: 들어간 면 + F16 글(+ 앞 그림 20px). 폭은 글 길이로 어림. 반환 = 알약 노드(폭은 rect.width).</summary>
        public static RectTransform Chip(GameContext ctx, RectTransform parent, float x, float y, string text, Sprite icon = null, Color? textColor = null)
        {
            const float pad = 12f, iconSz = 20f, iconGap = 6f;
            float tw = EstimateWidth(text, UiFontId.F16);
            float left = pad + (icon != null ? iconSz + iconGap : 0f);
            float w = Mathf.Ceil(left + tw + pad);
            var chip = Surface(ctx, parent, x, y, w, ChipH, 0);
            chip.name = "Chip";
            if (icon != null) UiKit.Image(chip, icon, pad, (ChipH - iconSz) * 0.5f, iconSz, iconSz);
            UiKit.Label(ctx, chip, text ?? string.Empty, UiFontId.F16, left, (ChipH - UiTheme.LineHeight(UiFontId.F16)) * 0.5f,
                tw + 4f, UiTheme.LineHeight(UiFontId.F16), textColor ?? UiTheme.Chalk, TextAnchor.MiddleLeft);
            return chip;
        }

        /// <summary>글 폭 어림(px). 한글·한자 등 넓은 글자 = 글자 크기, 그 밖 = 0.6배.</summary>
        public static float EstimateWidth(string text, UiFontId size)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float px = (int)size, w = 0f;
            foreach (char c in text) w += c >= 0x80 ? px : c == ' ' ? px * 0.4f : px * 0.62f;
            return w + px * 0.5f; // 글꼴마다 폭이 조금씩 달라 잘리지 않게 여유
        }

        // ------------------------------------------------------------------ 버튼

        /// <summary>주 버튼 자리(화면 기준): 오른쪽 아래, 아래 조작 안내 띠 위 24px.</summary>
        public static Rect PrimaryRect => new Rect(ScreenW - Outer - PrimaryW,
            ScreenH - ScreenBase.BottomBarHeight - PrimaryMargin - PrimaryH, PrimaryW, PrimaryH);

        /// <summary>주 버튼이 차지하는 아래 띠(화면 기준 y). 내용은 이 y 위에서 끝낸다.</summary>
        public static float PrimaryTop => PrimaryRect.y - PrimaryMargin;

        /// <summary>
        /// 주 버튼(화면마다 하나). 초록 Good 바탕 + 먹색(PaperText) F32 글. contentTop = parent의 화면 기준 위 y
        /// (ScreenBase의 Content면 ScreenBase.TopBarHeight, Root면 0). focus가 있으면 커서 묶음에 넣는다.
        /// </summary>
        public static UiButton PrimaryButton(GameContext ctx, RectTransform parent, string text, Action onClick,
            UiFocusGroup focus = null, float contentTop = ScreenBase.TopBarHeight)
        {
            var r = PrimaryRect;
            // Plain 꼴: 키트 그림 없이 상태만 얇게. 금색 바탕 금지 규칙과 무관하게 초록 면을 직접 깐다.
            var b = UiKit.Button(ctx, parent, text, r.x, r.y - contentTop, r.width, r.height, onClick,
                UiButtonStyle.Plain, focus, null, UiFontId.F32);
            var fillRoot = UiKit.Node(b.Rect, "PrimaryFill", 0f, 0f, r.width, r.height);
            fillRoot.SetSiblingIndex(1); // Background 위, Glow·글 아래.
            FillRounded(fillRoot, r.width, r.height, UiTheme.Good);
            UiKit.Rect(fillRoot, Corner, 0f, r.width - 2f * Corner, 2f, UiTheme.WithAlpha(UiTheme.Chalk, 0.35f), false, "Sheen");
            UiKit.Rect(fillRoot, Corner, r.height - 4f, r.width - 2f * Corner, 4f, UiTheme.WithAlpha(UiTheme.Ink, 0.35f), false, "Lip");
            fillRoot.gameObject.AddComponent<UiPrimaryFill>().Init(b, fillRoot);
            b.LabelColor = UiTheme.PaperText;
            b.Refresh();
            return b;
        }

        /// <summary>보조 버튼(조용한 꼴): 들어간 면 + Chalk F24 글. 좌표는 parent 기준.</summary>
        public static UiButton SecondaryButton(GameContext ctx, RectTransform parent, string text, float x, float y, float w,
            Action onClick, UiFocusGroup focus = null, float h = SecondaryH)
        {
            var b = UiKit.Button(ctx, parent, text, x, y, w, h, onClick, UiButtonStyle.Plain, focus, null, UiFontId.F24);
            var fillRoot = UiKit.Node(b.Rect, "SecondaryFill", 0f, 0f, w, h);
            fillRoot.SetSiblingIndex(0); // Background(상태색) 아래.
            FillRounded(fillRoot, w, h, UiTheme.WithAlpha(UiTheme.WoodDark, SurfaceAlpha));
            b.LabelColor = UiTheme.Chalk;
            b.Refresh();
            return b;
        }

        /// <summary>주 버튼 바로 왼쪽(같은 줄)에 보조 버튼을 둘 x(화면 기준). index 0이 가장 가깝다.</summary>
        public static float SecondaryXBesidePrimary(float w, int index = 0) =>
            PrimaryRect.x - (index + 1) * (w + ColGap);

        /// <summary>주 버튼과 같은 줄의 보조 버튼 y(parent 기준, 세로 가운데 맞춤).</summary>
        public static float SecondaryYBesidePrimary(float contentTop = ScreenBase.TopBarHeight, float h = SecondaryH) =>
            PrimaryRect.y + (PrimaryH - h) * 0.5f - contentTop;
    }

    /// <summary>주 버튼 초록 면을 꺼짐 상태에 맞춰 어둡게(표시만).</summary>
    sealed class UiPrimaryFill : MonoBehaviour
    {
        UiButton _button;
        UImage[] _imgs;
        Color[] _base;
        bool _lastEnabled = true;

        public void Init(UiButton button, RectTransform root)
        {
            _button = button;
            _imgs = root.GetComponentsInChildren<UImage>(true);
            _base = new Color[_imgs.Length];
            for (int i = 0; i < _imgs.Length; i++) _base[i] = _imgs[i].color;
        }

        void LateUpdate()
        {
            if (_button == null || _imgs == null) return;
            bool en = _button.Enabled;
            if (en == _lastEnabled) return;
            _lastEnabled = en;
            for (int i = 0; i < _imgs.Length; i++)
            {
                if (_imgs[i] == null) continue;
                Color c = en ? _base[i] : _base[i] * UiTheme.DisabledTint;
                c.a = _base[i].a;
                _imgs[i].color = c;
            }
        }
    }
}
