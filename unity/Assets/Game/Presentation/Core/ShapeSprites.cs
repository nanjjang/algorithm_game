#nullable enable
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wizard.Unity.Presentation.Core
{
    // 회색 도형의 바탕 그림 셋(흰 원판, 흰 사각, 흰 삼각)을 코드로 만든다. 그림 파일과 프리팹을 쓰지 않는다(T-32, 에셋 반입 전).
    // 각 그림은 1유닛(= 1칸) 크기이고 색은 SpriteRenderer.color 로 입힌다. 삼각형은 +x(오른쪽)를 가리킨다.
    // 가진 쪽(ShapeCanvas)이 Dispose 로 지운다. 정적 캐시를 두지 않는다.
    public sealed class ShapeSprites : IDisposable
    {
        public const int TextureSize = 64;                // 자료구조 크기(그림 한 변의 픽셀 수 = 1칸의 px)
        private const int SquareSize = 4;                 // 자료구조 크기

        private readonly Texture2D _diskTex;
        private readonly Texture2D _squareTex;
        private readonly Texture2D _triangleTex;

        public ShapeSprites()
        {
            _diskTex = NewTexture("shape.disk", TextureSize);
            _squareTex = NewTexture("shape.square", SquareSize);
            _triangleTex = NewTexture("shape.triangle", TextureSize);
            FillDisk(_diskTex);
            FillSquare(_squareTex);
            FillTriangle(_triangleTex);
            Disk = NewSprite(_diskTex);
            Square = NewSprite(_squareTex);
            Triangle = NewSprite(_triangleTex);
        }

        public Sprite Disk { get; }

        public Sprite Square { get; }

        public Sprite Triangle { get; }

        public void Dispose()
        {
            Destroy(Disk);
            Destroy(Square);
            Destroy(Triangle);
            Destroy(_diskTex);
            Destroy(_squareTex);
            Destroy(_triangleTex);
        }

        internal static void Destroy(Object? o)
        {
            if (o == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(o);
            }
            else
            {
                Object.DestroyImmediate(o);
            }
        }

        private static Texture2D NewTexture(string name, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            return t;
        }

        private static Sprite NewSprite(Texture2D t)
        {
            Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width);
            s.name = t.name;
            s.hideFlags = HideFlags.DontSave;
            return s;
        }

        // 가장자리 한 픽셀을 부드럽게 한 원판
        private static void FillDisk(Texture2D t)
        {
            int n = t.width;
            float r = n / 2f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, false);
        }

        private static void FillSquare(Texture2D t)
        {
            var px = new Color32[t.width * t.height];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255, 255);
            }
            t.SetPixels32(px);
            t.Apply(false, false);
        }

        // 왼쪽 변 전체에서 오른쪽 가운데 꼭짓점으로 좁아지는 삼각형
        private static void FillTriangle(Texture2D t)
        {
            int n = t.width;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;                       // 0(왼쪽) ~ 1(꼭짓점)
                    float v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f; // 0(가운데) ~ 1(위아래 끝)
                    bool inside = v <= 1f - u;
                    px[y * n + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }
            t.SetPixels32(px);
            t.Apply(false, false);
        }
    }
}
