using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.UI
{
    /// <summary>버튼 꼴(09 요약서 6.3절 UI 키트 ID).</summary>
    public enum UiButtonStyle
    {
        /// <summary>K-20 큰 버튼 48×64 9s12.</summary>
        Large = 0,
        /// <summary>K-21 보통 48×48.</summary>
        Medium = 1,
        /// <summary>K-22 작은 32×32 9s8.</summary>
        Small = 2,
        /// <summary>K-23 주 버튼 48×64(tint 필드: 속성색을 곱할 수 있다. 금색 금지).</summary>
        Primary = 3,
        /// <summary>K-24 조심 버튼 48×48: 호박색 테 + 경고 삼각(포기, 삭제, 떠나보내기). 빨강을 쓰지 않는다.</summary>
        Caution = 4,
        /// <summary>K-25 탭 48×48(normal/hover/on/disabled).</summary>
        Tab = 5,
        /// <summary>K-26 세로 탭 48×56(normal/hover/on).</summary>
        TabVertical = 6,
        /// <summary>바탕 그림 없음(목록 줄, 카드, 칸). 상태는 얇은 바탕색으로만, 괄호·이유 줄은 같다.</summary>
        Plain = 7
    }

    /// <summary>
    /// 버튼(UiKit.Button / IconButton이 만든다). 상태 5종(normal/hover/pressed/on/disabled) + 조심 버튼.
    /// 그림이 있으면 키트 그림의 상태 파일을, 없으면(ctx.Art가 null이거나 그림이 없음) 단색으로 그린다.
    /// 누름 = 내용 2px 아래로. 켜짐 = 바탕 #3A3F5A + 아랫변 분필 흰색 4px 줄. 꺼짐 = 그림 밝기 60%, 글자 회청.
    /// </summary>
    public sealed class UiButton : UiWidget
    {
        public UiButtonStyle Style;
        public UImage Background;
        public RectTransform ContentRoot;
        public UiLabel Label;
        public UImage Icon;
        /// <summary>조심 버튼의 테와 경고 삼각.</summary>
        public UiFrame CautionFrame;
        public UiTriangle CautionMark;
        /// <summary>켜짐 줄.</summary>
        public UImage OnBar;
        /// <summary>그림에 곱하는 색(K-23 속성색 등). 기본 흰색.</summary>
        public Color Tint = Color.white;
        /// <summary>글자 기본색.</summary>
        public Color LabelColor = UiTheme.Chalk;

        Vector2 _contentRest;
        bool _contentRestSet;

        /// <summary>올림·커서 빛(바탕 위, 내용 아래). 없으면 null.</summary>
        public UImage Glow;
        /// <summary>올림·누름 때 살짝 커지고 줄어드는가(목록 줄·카드·탭 같은 Plain/Tab 꼴은 기본 꺼짐 — 빛만).</summary>
        public bool HoverMotion = true;
        float _scaleNow = 1f, _scaleTarget = 1f, _glowNow, _glowTarget;

        public static string KitId(UiButtonStyle s)
        {
            switch (s)
            {
                case UiButtonStyle.Large: return "K-20";
                case UiButtonStyle.Medium: return "K-21";
                case UiButtonStyle.Small: return "K-22";
                case UiButtonStyle.Primary: return "K-23";
                case UiButtonStyle.Caution: return "K-24";
                case UiButtonStyle.Tab: return "K-25";
                case UiButtonStyle.TabVertical: return "K-26";
                default: return null;
            }
        }

        public static string KitVariant(UiButtonStyle s)
        {
            switch (s)
            {
                case UiButtonStyle.Large: return "button_large";
                case UiButtonStyle.Medium: return "button_medium";
                case UiButtonStyle.Small: return "button_small";
                case UiButtonStyle.Primary: return "button_primary";
                case UiButtonStyle.Caution: return "button_caution";
                case UiButtonStyle.Tab: return "tab";
                case UiButtonStyle.TabVertical: return "tab_vertical";
                default: return null;
            }
        }

        public bool IsCaution => Style == UiButtonStyle.Caution;

        /// <summary>글을 바꾼다(이미 이름 표를 거친 글).</summary>
        public void SetText(string text) { if (Label != null) Label.SetText(text); }

        protected override void ApplyVisual(UiVisualState s)
        {
            if (ContentRoot != null)
            {
                if (!_contentRestSet) { _contentRest = ContentRoot.anchoredPosition; _contentRestSet = true; }
                ContentRoot.anchoredPosition = _contentRest + (s == UiVisualState.Pressed ? new Vector2(0f, -2f) : Vector2.zero);
            }
            if (Background != null) ApplyBackground(s);
            if (OnBar != null) OnBar.gameObject.SetActive(s == UiVisualState.On || (On && s == UiVisualState.Pressed));
            if (Label != null) TintLabel(Label.Text, s, LabelColor);
            if (Icon != null) Icon.color = s == UiVisualState.Disabled ? UiTheme.DisabledTint : Color.white;
            if (CautionFrame != null) CautionFrame.Color = s == UiVisualState.Disabled ? UiTheme.SlateBlue : UiTheme.Amber;
            if (CautionMark != null) CautionMark.color = s == UiVisualState.Disabled ? UiTheme.SlateBlue : UiTheme.Amber;
            UpdateMotionTargets(s);
        }

        bool ScalesOnHover => HoverMotion && Style != UiButtonStyle.Plain && Style != UiButtonStyle.Tab && Style != UiButtonStyle.TabVertical;

        void UpdateMotionTargets(UiVisualState s)
        {
            bool lit = s != UiVisualState.Disabled && (s == UiVisualState.Hover || (Cursor && Mode == UiInputMode.Keyboard));
            _glowTarget = s == UiVisualState.Pressed ? 0.6f : lit ? 1f : 0f;
            _scaleTarget = !ScalesOnHover || s == UiVisualState.Disabled ? 1f
                : s == UiVisualState.Pressed ? UiTheme.PressScale
                : lit ? UiTheme.HoverScale : 1f;
        }

        /// <summary>올림·누름 연출(표시만). 에디터 모드에서는 돌지 않아 배율 1·빛 0 그대로다.</summary>
        protected override void Update()
        {
            base.Update();
            float k = 1f - Mathf.Exp(-UiTheme.HoverLerpSpeed * Time.unscaledDeltaTime);
            if (Mathf.Abs(_scaleNow - _scaleTarget) > 0.0005f)
            {
                _scaleNow = Mathf.Lerp(_scaleNow, _scaleTarget, k);
                if (Mathf.Abs(_scaleNow - _scaleTarget) <= 0.0005f) _scaleNow = _scaleTarget;
                transform.localScale = new Vector3(_scaleNow, _scaleNow, 1f);
            }
            if (Glow != null && Mathf.Abs(_glowNow - _glowTarget) > 0.002f)
            {
                _glowNow = Mathf.Lerp(_glowNow, _glowTarget, k);
                var c = UiTheme.HoverGlow;
                c.a *= _glowNow;
                Glow.color = c;
            }
        }

        void OnDisable()
        {
            _scaleNow = 1f;
            _glowNow = 0f;
            if (this != null) transform.localScale = Vector3.one;
            if (Glow != null) Glow.color = new Color(0f, 0f, 0f, 0f);
        }

        void ApplyBackground(UiVisualState s)
        {
            string id = KitId(Style);
            UiSprite sp = default;
            if (id != null)
            {
                sp = UiKit.KitSprite(Ctx, id, KitVariant(Style), UiStateRules.StateToken(s));
                if (!sp.IsValid && s != UiVisualState.Normal)
                {
                    var fb = UiStateRules.Fallback(s);
                    sp = UiKit.KitSprite(Ctx, id, KitVariant(Style), UiStateRules.StateToken(fb));
                    if (!sp.IsValid && fb != UiVisualState.Normal) sp = UiKit.KitSprite(Ctx, id, KitVariant(Style), null);
                }
            }
            if (sp.IsValid)
            {
                UiKit.SetSprite(Background, sp);
                Color c = Tint;
                if (s == UiVisualState.Disabled) c = c * UiTheme.DisabledTint;
                c.a = Tint.a;
                Background.color = c;
            }
            else
            {
                Background.sprite = null;
                Background.type = UImage.Type.Simple;
                Background.color = FallbackColor(s);
            }
        }

        Color FallbackColor(UiVisualState s)
        {
            if (Style == UiButtonStyle.Plain)
            {
                switch (s)
                {
                    case UiVisualState.Hover: return UiTheme.WithAlpha(UiTheme.WoodLight, 0.6f);
                    case UiVisualState.Pressed: return UiTheme.WithAlpha(UiTheme.WoodDark, 0.6f);
                    case UiVisualState.On: return UiTheme.WoodLight;
                    default: return new Color(0f, 0f, 0f, 0f);
                }
            }
            switch (s)
            {
                case UiVisualState.Hover: return UiTheme.WoodLight;
                case UiVisualState.Pressed: return UiTheme.WoodDark;
                case UiVisualState.On: return UiTheme.WoodLight;
                case UiVisualState.Disabled: return UiTheme.Wood * UiTheme.DisabledTint;
                default: return UiTheme.Wood;
            }
        }

        /// <summary>UiKit.Button이 부른다: 하위 그림을 만들고 위젯을 시작한다.</summary>
        internal void InitWidgetParts(GameContext ctx, string text, UiFontId fontSize, Sprite icon)
        {
            Ctx = ctx;
            BuildParts(text, fontSize, icon);
            InitWidget(ctx);
        }

        void BuildParts(string text, UiFontId fontSize, Sprite icon)
        {
            var rt = Rect;
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(UImage));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(rt, false);
            UiKit.Stretch(bgRt);
            Background = bgGo.GetComponent<UImage>();
            Background.raycastTarget = true;

            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(UImage));
            var glowRt = (RectTransform)glowGo.transform;
            glowRt.SetParent(rt, false);
            UiKit.Stretch(glowRt, 2f, 2f, 2f, 2f);
            Glow = glowGo.GetComponent<UImage>();
            Glow.raycastTarget = false;
            Glow.color = new Color(0f, 0f, 0f, 0f);

            if (Style == UiButtonStyle.Caution)
            {
                CautionFrame = UiFrame.Create(rt, UiTheme.Amber, 2);
                CautionFrame.name = "CautionFrame";
            }

            var onGo = new GameObject("OnBar", typeof(RectTransform), typeof(UImage));
            var ort = (RectTransform)onGo.transform;
            ort.SetParent(rt, false);
            ort.anchorMin = new Vector2(0f, 0f);
            ort.anchorMax = new Vector2(1f, 0f);
            ort.pivot = new Vector2(0.5f, 0f);
            ort.sizeDelta = new Vector2(0f, 4f);
            ort.anchoredPosition = Vector2.zero;
            OnBar = onGo.GetComponent<UImage>();
            OnBar.color = UiTheme.Chalk;
            OnBar.raycastTarget = false;
            onGo.SetActive(false);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            ContentRoot = (RectTransform)contentGo.transform;
            ContentRoot.SetParent(rt, false);
            UiKit.Stretch(ContentRoot);

            float left = 0f;
            Vector2 size = rt.rect.size;
            if (Style == UiButtonStyle.Caution)
            {
                float tri = Mathf.Min(24f, Mathf.Max(12f, size.y * 0.5f));
                var triGo = new GameObject("CautionMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiTriangle));
                var trt = (RectTransform)triGo.transform;
                trt.SetParent(ContentRoot, false);
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.sizeDelta = new Vector2(tri, tri);
                trt.anchoredPosition = new Vector2(12f, 0f);
                CautionMark = triGo.GetComponent<UiTriangle>();
                CautionMark.InnerMark = true;
                CautionMark.color = UiTheme.Amber;
                CautionMark.raycastTarget = false;
                left = 12f + tri + 4f;
            }
            if (icon != null)
            {
                float isz = Mathf.Min(size.y - 8f, icon.rect.height);
                if (isz <= 0f) isz = size.y;
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(UImage));
                var irt = (RectTransform)iconGo.transform;
                irt.SetParent(ContentRoot, false);
                bool iconOnly = string.IsNullOrEmpty(text);
                irt.anchorMin = irt.anchorMax = iconOnly ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                irt.pivot = iconOnly ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                irt.sizeDelta = new Vector2(isz * icon.rect.width / Mathf.Max(1f, icon.rect.height), isz);
                irt.anchoredPosition = iconOnly ? Vector2.zero : new Vector2(left + 8f, 0f);
                Icon = iconGo.GetComponent<UImage>();
                Icon.sprite = icon;
                Icon.preserveAspect = true;
                Icon.raycastTarget = false;
                if (!iconOnly) left += 8f + irt.sizeDelta.x;
            }
            if (!string.IsNullOrEmpty(text) || icon == null)
            {
                float pad = 8f;
                float lh = UiTheme.LineHeight(fontSize);
                Label = UiKit.Label(Ctx, ContentRoot, text ?? string.Empty, fontSize, left + pad, (size.y - lh) * 0.5f,
                    Mathf.Max(1f, size.x - left - 2f * pad), lh, LabelColor, left > 0f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                var lrt = Label.Rect;
                // 높이가 바뀌어도 가운데에 머물게 세로 가운데 고정.
                lrt.anchorMin = new Vector2(0f, 0.5f);
                lrt.anchorMax = new Vector2(1f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = new Vector2(left + pad, -lh * 0.5f);
                lrt.offsetMax = new Vector2(-pad, lh * 0.5f);
                Label.Refit();
            }
        }

        public UText LabelText => Label != null ? Label.Text : null;
    }
}
