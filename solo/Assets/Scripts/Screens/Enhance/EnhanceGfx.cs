using System;
using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면 전용 코드 생성 도형(부드러운 원, 고리, 빛기둥, 가장자리 그늘, 둥근 판, 유리 조각).
    /// 그림 원본이 아니라 빛·판을 흉내 내는 수학 텍스처다(새 그림을 그리지 않는다 — solo/CLAUDE.md).
    /// 텍스처는 한 번 만들어 둔다. HideAndDontSave라 장면 정리(UnloadUnusedAssets)에 내려가지 않고, 내려갔으면 다시 만든다.
    /// 가산 합성(빛 번짐 흉내)은 그림 창고의 Solo/SpriteAdditive 머티리얼을 UI 그림에 씌운다(Shader.Find 금지 — Services.cs).
    /// </summary>
    public static class EnhanceGfx
    {
        static Sprite _softDot, _glow, _ring, _ringThin, _beam, _vignette, _round, _roundBorder, _shard, _sparkle, _sheen;

        public static Sprite SoftDot => Get(ref _softDot, () => Radial(64, r => Smooth(1f - r) * Smooth(1f - r)));
        public static Sprite Glow => Get(ref _glow, () => Radial(256, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)));
        public static Sprite Ring => Get(ref _ring, () => Radial(256, r => Band(r, 0.86f, 0.06f)));
        public static Sprite RingThin => Get(ref _ringThin, () => Radial(256, r => Band(r, 0.93f, 0.025f)));
        public static Sprite Beam => Get(ref _beam, MakeBeam);
        public static Sprite Vignette => Get(ref _vignette, () => Radial(256, r => Mathf.Pow(Mathf.Clamp01((r - 0.45f) / 0.75f), 1.6f)));
        public static Sprite Round => Get(ref _round, () => RoundRect(64, 14, false));
        public static Sprite RoundBorder => Get(ref _roundBorder, () => RoundRect(64, 14, true));
        public static Sprite Shard => Get(ref _shard, MakeShard);
        public static Sprite Sparkle => Get(ref _sparkle, MakeSparkle);
        public static Sprite Sheen => Get(ref _sheen, MakeSheen);

        static Sprite Get(ref Sprite slot, Func<Sprite> make)
        {
            if (slot == null) slot = make();   // 유니티 null(내려간 텍스처)도 다시 만든다
            return slot;
        }

        // ------------------------------------------------------------------ 배치 도우미

        /// <summary>부모의 왼쪽 위 원점 기준 (cx, cy)를 가운데로 w×h(피벗 가운데 — 돌리고 키워도 제자리).</summary>
        public static RectTransform Centered(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
            return rt;
        }

        /// <summary>가운데 피벗 그림 하나(마우스를 막지 않는다).</summary>
        public static UImage Img(RectTransform parent, Sprite sprite, float cx, float cy, float w, float h, Color color, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Centered(rt, cx, cy, w, h);
            var img = go.GetComponent<UImage>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>둥근 판(9칸 늘림). border면 테두리만.</summary>
        public static UImage Panel(RectTransform parent, float x, float y, float w, float h, Color color, bool border, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            var img = go.GetComponent<UImage>();
            img.sprite = border ? RoundBorder : Round;
            img.type = UImage.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>가산 머티리얼을 씌운다(그림 창고가 없거나 실패하면 알파 혼합 그대로).</summary>
        public static void Additive(GameContext ctx, UImage img)
        {
            if (img == null || ctx == null || ctx.Art == null) return;
            try
            {
                var m = ctx.Art.Material(SpriteMaterialKind.SpriteAdditive);
                if (m != null) img.material = m;
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ 텍스처 만들기

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static float Band(float r, float center, float half)
        {
            float d = Mathf.Abs(r - center) / half;
            return d >= 1f ? 0f : Smooth(1f - d);
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = "EnhanceGfx"
            };
            return t;
        }

        static Sprite ToSprite(Texture2D t, Color32[] px, Vector4 border = default)
        {
            t.SetPixels32(px);
            t.Apply(false, false);
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.HideAndDontSave;
            s.name = "EnhanceGfx";
            return s;
        }

        static Color32 White(float a) => new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));

        /// <summary>가운데에서 거리 r(0 가운데, 1 가장자리 내접원)의 함수로 알파를 그린다.</summary>
        static Sprite Radial(int size, Func<float, float> alpha)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    px[y * size + x] = White(alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            return ToSprite(t, px);
        }

        static Sprite MakeBeam()
        {
            int w = 64, h = 256;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - (w - 1) * 0.5f) / ((w - 1) * 0.5f);
                    float v = y / (float)(h - 1);
                    float across = Mathf.Exp(-u * u * 6f) * 0.75f + Mathf.Exp(-u * u * 40f) * 0.25f;
                    float along = Smooth(v * 4f) * Smooth((1f - v) * 2.2f);
                    px[y * w + x] = White(across * along);
                }
            return ToSprite(t, px);
        }

        static Sprite RoundRect(int size, int radius, bool borderOnly)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - outside);
                    if (borderOnly) a *= Mathf.Clamp01(outside + 2.5f);
                    px[y * size + x] = White(a);
                }
            return ToSprite(t, px, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        /// <summary>유리 조각(뾰족한 사각형).</summary>
        static Sprite MakeShard()
        {
            int s = 32;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            Vector2 a = new Vector2(16, 31), b = new Vector2(27, 9), c = new Vector2(4, 2), d = new Vector2(7, 20);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = InTri(p, a, b, c) || InTri(p, a, c, d);
                    float edge = inside ? 0.55f + 0.45f * Mathf.Clamp01((p.x - 4f) / 24f) : 0f;
                    px[y * s + x] = White(edge);
                }
            return ToSprite(t, px);
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        /// <summary>네 갈래 반짝임.</summary>
        static Sprite MakeSparkle()
        {
            int s = 64;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                    float arm = Mathf.Max(Mathf.Exp(-dy * dy * 400f) * (1f - dx), Mathf.Exp(-dx * dx * 400f) * (1f - dy));
                    float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.5f), 2f);
                    px[y * s + x] = White(arm + core);
                }
            return ToSprite(t, px);
        }

        /// <summary>위가 밝고 아래로 사라지는 막(판의 윤기).</summary>
        static Sprite MakeSheen()
        {
            int w = 4, h = 64;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = White(Mathf.Pow(y / (float)(h - 1), 2f));
            return ToSprite(t, px);
        }
    }
}
