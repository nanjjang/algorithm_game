using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 가로 게이지 하나(과열, 웨이브 진행, 보스 체력). 공통 HUD 그림(gauge_frame/gauge_fill 256×32)이 있으면 틀 위에 회색조 채움을
    /// 색 입혀 포개고 가로 마스크(RectMask2D)로 자른다. 없으면 먹색 테 + 바탕 + 단색 채움으로 그린다(기존 모습).
    /// Fill은 "채움 폭 = 폭 × 비율"인 칸이다(그림이 있으면 마스크 칸) — 테스트·잔상·끝 빛이 이 폭을 읽는다.
    /// 그림 높이(32)는 늘리지 않는다(정수배 규칙). 가로는 좌우 끝만 고정한 9조각이라 늘어나도 장식이 찌그러지지 않는다.
    /// </summary>
    public sealed class HudGauge
    {
        public readonly RectTransform Root;
        /// <summary>채움 칸(폭 = W × 비율).</summary>
        public readonly UImage Fill;
        /// <summary>잔상 칸(폭 = W × 잔상 비율). withTrail일 때만.</summary>
        public readonly UImage Trail;
        readonly UImage _fillArt, _trailArt;
        public readonly float W, H;
        public readonly bool HasArt;
        /// <summary>
        /// 그림 채움의 안쪽 위아래 여백(gauge_fill 256×32의 불투명 칸 = y 9~23). 가로는 마스크를 게이지 전체 폭에 걸어
        /// "채움 칸 폭 = 폭 × 비율"을 지킨다(좌우 끝 8px의 투명 테두리만큼 처음·끝 몇 %가 덜 보일 뿐 0과 1은 정확하다).
        /// </summary>
        public const float ArtInsetY = 9f;
        public float InsetX => 0f;
        public float InsetY => HasArt ? ArtInsetY : 0f;

        public HudGauge(RectTransform parent, string name, float x, float y, float w, float flatH, Color fill,
            UiSprite railKit = default, UiSprite fillKit = default, bool withTrail = false, Color? trailColor = null, IArtLibrary lib = null)
        {
            var frame = UiCommonArt.GaugeFrameSprite(lib);
            var fillSp = UiCommonArt.GaugeFillSprite(lib);
            HasArt = frame != null && fillSp != null;
            W = w;
            H = HasArt ? UiCommonArt.GaugeH : flatH;
            // 그림이 있으면 32 높이를 flatH 칸의 가운데에 맞춘다.
            float yy = HasArt ? y - (UiCommonArt.GaugeH - flatH) * 0.5f : y;
            Root = UiKit.Node(parent, name, x, yy, w, H);
            Color tc = trailColor ?? UiTheme.WithAlpha(UiTheme.Paper, 0.55f);
            if (HasArt)
            {
                var fr = UiKit.Rect(Root, 0, 0, w, H, Color.white, false, "Frame");
                UiCommonArt.ApplySliced(fr, frame);
                fr.color = Color.white;
                if (withTrail) Trail = Masked(Root, "Trail", w, H, fillSp, tc, out _trailArt);
                Fill = Masked(Root, "Fill", w, H, fillSp, fill, out _fillArt);
            }
            else
            {
                UiKit.Rect(Root, -2, -2, w + 4, H + 4, UiTheme.Ink, false, "Rim");
                var rail = UiKit.Rect(Root, 0, 0, w, H, HudColors.BarRail, false, "Rail");
                HudArt.SetKit(rail, railKit, HudColors.BarRail);
                if (withTrail) Trail = UiKit.Rect(Root, 0, 0, w, H, tc, false, "Trail");
                Fill = UiKit.Rect(Root, 0, 0, w, H, fill, false, "Fill");
                HudArt.SetKit(Fill, fillKit, fill);
                if (!fillKit.IsValid) Fill.color = fill;
            }
        }

        static UImage Masked(RectTransform parent, string name, float w, float h, Sprite sp, Color c, out UImage art)
        {
            // 마스크 = 왼쪽에 붙어 폭이 줄어드는 칸. 그림은 틀과 같은 자리에 포갠다.
            var mask = UiKit.Rect(parent, 0, 0, w, h, new Color(0f, 0f, 0f, 0f), false, name);
            mask.gameObject.AddComponent<RectMask2D>();
            art = UiKit.Rect(mask.rectTransform, 0, 0, w, h, c, false, "Art");
            UiCommonArt.ApplySliced(art, sp);
            art.color = c;
            return mask;
        }

        /// <summary>채움이 차지할 수 있는 폭(그림이면 안쪽 칸 폭).</summary>
        public float Span => W - 2f * InsetX;

        /// <summary>채움 비율(0~1)을 놓는다. 돌려주는 값 = 채움 오른쪽 끝의 x(Root 기준).</summary>
        public float SetRatio(float ratio)
        {
            float fw = Mathf.Round(Span * Mathf.Clamp01(ratio));
            Fill.rectTransform.sizeDelta = new Vector2(fw, H);
            HudArt.SetActive(Fill, fw >= 0.5f);
            return InsetX + fw;
        }

        public void SetTrail(float ratio)
        {
            if (Trail == null) return;
            float tw = Mathf.Round(Span * Mathf.Clamp01(ratio));
            Trail.rectTransform.sizeDelta = new Vector2(tw, H);
        }

        public void SetColor(Color c)
        {
            if (_fillArt != null) _fillArt.color = c;
            else if (Fill.sprite == null) Fill.color = c;
        }
    }
}
