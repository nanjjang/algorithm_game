using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 그림 없이 만드는 화면 바탕 꾸밈: 세로 그라데이션, 둥근 빛 번짐, 가장자리 비네트. 작은 흑백(알파) 텍스처 세 장을 코드로 만들어
    /// 한 번만 들고(앱 수명 동안 같은 것을 쓴다), 색은 Image.color로 입힌다. 그림 에셋을 새로 만드는 것이 아니라 단순 도형의 번짐이다.
    /// </summary>
    public static class UiBackdrop
    {
        const int GradientH = 64, RadialSize = 64;
        static Sprite _vertical, _radialSoft, _vignette, _solid;

        /// <summary>흰 4×4 한 장(Filled 이미지·부채꼴 채움처럼 스프라이트가 있어야 하는 단색 칸).</summary>
        public static Sprite Solid
        {
            get
            {
                if (_solid != null) return _solid;
                var tex = NewTex(4, 4);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                return _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        /// <summary>위 불투명(알파 1) → 아래 투명(알파 0) 세로 띠.</summary>
        public static Sprite VerticalFade => _vertical != null ? _vertical : (_vertical = MakeVertical());
        /// <summary>가운데 불투명 → 가장자리 투명의 둥근 번짐.</summary>
        public static Sprite RadialSoft => _radialSoft != null ? _radialSoft : (_radialSoft = MakeRadial(false));
        /// <summary>가운데 투명 → 가장자리 불투명의 비네트.</summary>
        public static Sprite Vignette => _vignette != null ? _vignette : (_vignette = MakeRadial(true));

        /// <summary>
        /// 화면 바탕 한 벌(밤색 바탕 + 위에서 내려오는 푸른 빛 + 위 가운데 빛 번짐 + 비네트)을 parent에 꽉 채워 깐다. 마우스를 막는다(맨 아래 판).
        /// </summary>
        public static RectTransform Screen(RectTransform parent, bool blockRaycast = true, float vignetteAlpha = 0.7f)
        {
            var root = UiKit.Node(parent, "Backdrop");
            UiKit.Stretch(root);
            var baseImg = UiKit.Rect(root, 0f, 0f, 0f, 0f, UiTheme.Night, blockRaycast, "Base");
            UiKit.Stretch(baseImg.rectTransform);
            var top = Fill(root, "TopLight", VerticalFade, UiTheme.WithAlpha(UiTheme.NightTop, 1f));
            UiKit.Stretch(top.rectTransform, 0f, 0f, 0f, 0f);
            top.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            var glow = Fill(root, "Glow", RadialSoft, UiTheme.WithAlpha(UiTheme.WoodLight, 0.35f));
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(1600f, 900f);
            glow.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            var vig = Fill(root, "Vignette", Vignette, UiTheme.WithAlpha(UiTheme.Ink, vignetteAlpha));
            UiKit.Stretch(vig.rectTransform);
            return root;
        }

        /// <summary>그림 위에 얹는 비네트만(가장자리를 어둡게).</summary>
        public static UImage VignetteOver(RectTransform parent, float alpha = 0.6f)
        {
            var v = Fill(parent, "Vignette", Vignette, UiTheme.WithAlpha(UiTheme.Ink, alpha));
            UiKit.Stretch(v.rectTransform);
            return v;
        }

        /// <summary>둥근 빛 번짐 한 장(제목·카드 뒤 강조). 왼쪽 위 원점 px.</summary>
        public static UImage Glow(RectTransform parent, float x, float y, float w, float h, Color color)
        {
            var g = Fill(parent, "Glow", RadialSoft, color);
            UiKit.Place(g.rectTransform, x, y, w, h);
            return g;
        }

        /// <summary>세로 그라데이션 한 장(위 color → 아래 투명). 왼쪽 위 원점 px.</summary>
        public static UImage Fade(RectTransform parent, float x, float y, float w, float h, Color color, bool flip = false)
        {
            var g = Fill(parent, "Fade", VerticalFade, color);
            UiKit.Place(g.rectTransform, x, y, w, h);
            if (flip)
            {
                // 아래 불투명 → 위 투명: 가운데를 축으로 위아래를 뒤집는다.
                g.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                g.rectTransform.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
                g.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            }
            return g;
        }

        static UImage Fill(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var img = UiKit.Rect(parent, 0f, 0f, 0f, 0f, color, false, name);
            img.sprite = sprite;
            img.type = UImage.Type.Simple;
            img.preserveAspect = false;
            return img;
        }

        static Sprite MakeVertical()
        {
            var tex = NewTex(1, GradientH);
            var px = new Color32[GradientH];
            for (int y = 0; y < GradientH; y++)
            {
                // y = 0이 아래. 위가 불투명.
                float k = y / (float)(GradientH - 1);
                px[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * k * k * (3f - 2f * k)));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, GradientH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeRadial(bool vignette)
        {
            var tex = NewTex(RadialSize, RadialSize);
            var px = new Color32[RadialSize * RadialSize];
            float c = (RadialSize - 1) * 0.5f;
            for (int y = 0; y < RadialSize; y++)
                for (int x = 0; x < RadialSize; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (vignette) { float k = Mathf.Clamp01((r - 0.55f) / 0.85f); a = k * k * (3f - 2f * k); }
                    else { float k = Mathf.Clamp01(1f - r); a = k * k * (3f - 2f * k); }
                    px[y * RadialSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, RadialSize, RadialSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "UiBackdrop"
            };
            return tex;
        }
    }
}
