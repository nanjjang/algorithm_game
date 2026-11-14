using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// 코드로 만드는 그림의 규칙(엔진 텍스처 없이 시험한다). 대체 그림 = 회청 #8E9AAE · 나무 어두움 #1F2130 8px 바둑판(S14 5.7, 09 1절).
    /// 위험색·금색·연두·청록을 쓰지 않는다.
    /// </summary>
    public static class GeneratedArt
    {
        public static readonly Color32 Slate = new Color32(0x8E, 0x9A, 0xAE, 0xFF);
        public static readonly Color32 WoodDark = new Color32(0x1F, 0x21, 0x30, 0xFF);
        public const int CheckerPx = 8;
        /// <summary>코드로 만드는 텍스처의 최대 변(원하는 크기가 이보다 크면 줄인다).</summary>
        public const int MaxGeneratedPx = 4096;

        /// <summary>바둑판 칸: (x/8 + y/8)가 짝수면 회청. x, y는 왼쪽 아래 원점(텍스처 좌표).</summary>
        public static Color32 PlaceholderPixel(int x, int y)
        {
            return ((x / CheckerPx + y / CheckerPx) & 1) == 0 ? Slate : WoodDark;
        }

        /// <summary>
        /// 원·고리의 한 픽셀이 칠해지는가. 지름 2r 칸, 픽셀 중심 (x + 0.5, y + 0.5)와 원 중심 (r, r)의 거리 d로:
        /// 원 = d ≤ r, 고리 = r − 폭 ≤ d ≤ r. 경계는 계단(픽셀 그림, 보간 없음).
        /// </summary>
        public static bool CircleCovers(int x, int y, int radiusPx, bool ringOnly, int ringWidthPx)
        {
            float dx = x + 0.5f - radiusPx, dy = y + 0.5f - radiusPx;
            float d2 = dx * dx + dy * dy;
            float r = radiusPx;
            if (d2 > r * r) return false;
            if (!ringOnly) return true;
            float inner = r - (ringWidthPx < 1 ? 1 : ringWidthPx);
            return inner <= 0f || d2 >= inner * inner;
        }

        public static int Clamp(int px) => px < 1 ? 1 : px > MaxGeneratedPx ? MaxGeneratedPx : px;
    }

    public sealed partial class ArtLibrary
    {
        readonly Dictionary<long, Sprite> _placeholders = new Dictionary<long, Sprite>();
        readonly Dictionary<long, Sprite> _circles = new Dictionary<long, Sprite>();
        Sprite _white;
        readonly Material[] _materials = new Material[3];

        public Sprite Placeholder(int widthPx, int heightPx)
        {
            int w = GeneratedArt.Clamp(widthPx), h = GeneratedArt.Clamp(heightPx);
            long key = ((long)w << 32) | (uint)h;
            if (_placeholders.TryGetValue(key, out var s) && s != null) return s;
            var tex = NewTexture(w, h, "placeholder_" + w + "x" + h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = GeneratedArt.PlaceholderPixel(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), Center, RectMath.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            _placeholders[key] = s;
            return s;
        }

        /// <summary>1×1 흰 점. **PPU 1**: localScale이 곧 칸 수(월드 1칸 = 1 단위), UI Image에서는 크기와 무관하다.</summary>
        public Sprite WhitePixel
        {
            get
            {
                if (_white != null) return _white;
                var tex = NewTexture(1, 1, "white_pixel");
                tex.SetPixels32(new[] { new Color32(0xFF, 0xFF, 0xFF, 0xFF) });
                tex.Apply(false, true);
                _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), Center, 1f, 0, SpriteMeshType.FullRect);
                _white.name = tex.name;
                return _white;
            }
        }

        /// <summary>반지름 px의 흰 원(또는 고리). 지름 2r px, PPU 64, pivot 가운데. 색은 쓰는 쪽이 입힌다.</summary>
        public Sprite Circle(int radiusPx, bool ringOnly = false, int ringWidthPx = 2)
        {
            int r = GeneratedArt.Clamp(radiusPx);
            if (r * 2 > GeneratedArt.MaxGeneratedPx) r = GeneratedArt.MaxGeneratedPx / 2;
            int rw = ringOnly ? (ringWidthPx < 1 ? 1 : ringWidthPx) : 0;
            long key = ((long)r << 32) | ((long)rw << 1) | (ringOnly ? 1L : 0L);
            if (_circles.TryGetValue(key, out var s) && s != null) return s;
            int d = r * 2;
            var tex = NewTexture(d, d, "circle_" + r + (ringOnly ? "_ring" + rw : ""));
            var px = new Color32[d * d];
            var on = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
            var off = new Color32(0xFF, 0xFF, 0xFF, 0x00);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                    px[y * d + x] = GeneratedArt.CircleCovers(x, y, r, ringOnly, rw) ? on : off;
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, d, d), Center, RectMath.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            _circles[key] = s;
            return s;
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        // ---------- 셰이더·머티리얼 (00 문서 7·10절, D-70) ----------

        /// <summary>Resources/Shaders 아래 솔로 셰이더(이름 = 셰이더 파일 이름).</summary>
        public static string ShaderResourcePath(SpriteMaterialKind kind)
        {
            switch (kind)
            {
                case SpriteMaterialKind.SpriteAdditive: return "Shaders/SoloSpriteAdditive";
                case SpriteMaterialKind.Shape: return "Shaders/SoloShape";
                default: return "Shaders/SoloSprite";
            }
        }

        /// <summary>셰이더를 못 읽었을 때의 대체(빌드에 늘 들어가는 내장 셰이더 이름). Shader.Find는 Art 폴더에서만 쓴다(compile.sh SOLO0002).</summary>
        static readonly string[] FallbackShaders = { "Sprites/Default", "Unlit/Color", "Hidden/InternalErrorShader" };

        /// <summary>공유 머티리얼(종류마다 하나, 캐시). 개체별 값은 MaterialPropertyBlock + SpriteShaderProps로. null을 주지 않는다.</summary>
        public Material Material(SpriteMaterialKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= _materials.Length) i = 0;
            if (_materials[i] != null) return _materials[i];
            var shader = _src.LoadShader(ShaderResourcePath((SpriteMaterialKind)i));
            if (shader == null || !shader.isSupported)
            {
                Warn("셰이더 없음(대체 사용): " + ShaderResourcePath((SpriteMaterialKind)i));
                shader = null;
                foreach (var n in FallbackShaders)
                {
                    shader = Shader.Find(n);
                    if (shader != null) break;
                }
            }
            var m = shader != null ? new Material(shader) : new Material(Shader.Find(FallbackShaders[0]));
            m.name = "Solo" + (SpriteMaterialKind)i;
            m.hideFlags = HideFlags.DontSave;
            if (m.HasProperty(SpriteShaderProps.FlashAmount)) m.SetFloat(SpriteShaderProps.FlashAmount, 0f);
            if (m.HasProperty(SpriteShaderProps.OutlineSize)) m.SetFloat(SpriteShaderProps.OutlineSize, 0f);
            // GPU 인스턴싱을 켜지 않는다: 켜면 SpriteRenderer.color가 인스턴스 배열로 넘어오는데 Linear 색 공간에서 선형으로 바뀌지 않은
            // 값이 온다(Unity 6000.3 — 엔진의 Sprites/Default와 URP Sprite-Unlit도 같다). 위험색 #FF3B30이 #FF8478(분홍)로 뜬다.
            // 끄면 색이 정점 색으로 구워져(선형으로 바뀌어) 오고 스프라이트는 동적 배치로 묶인다. PlayMode의 SoloShaderColorTests가 지킨다.
            m.enableInstancing = false;
            _materials[i] = m;
            return m;
        }
    }
}
