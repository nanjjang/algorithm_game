using UnityEngine;

namespace Wizard.Solo.View
{
    /// <summary>
    /// 코드로 만든 연출용 작은 그림(그림 창고에 없는 것 — 부드러운 빛 점, 반짝 별, 화면 밖 화살표).
    /// 셰이더·머티리얼은 만들지 않는다(00 문서 7절): 텍스처와 Sprite만 만들고, 머티리얼은 풀이 넣는다.
    /// 판이 바뀌어도 같은 것을 쓴다(처음 쓸 때 한 번 만들고, 밖에서 부서졌으면 다시 만든다).
    /// 원본 그림이 생기면(보고서의 "있으면 좋은 그림" 목록) 이 대체를 걷어 낸다.
    /// </summary>
    public static class ProcSprites
    {
        /// <summary>그림 창고와 같은 PPU(1칸 = 64px).</summary>
        public const float Ppu = 64f;

        static Sprite _softDot, _star, _arrow, _hardDot;

        /// <summary>가운데가 밝고 가장자리로 부드럽게 사라지는 빛 점 32×32(반지름 0.25칸). 가산 혼합 한도(32px) 안.</summary>
        public static Sprite SoftDot => _softDot != null ? _softDot : (_softDot = MakeSoftDot(32));

        /// <summary>네 갈래 반짝 별 16×16.</summary>
        public static Sprite Star => _star != null ? _star : (_star = MakeStar(16));

        /// <summary>오른쪽을 가리키는 화살 촉 24×24(가장자리 표시). 회전으로 방향을 준다.</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = MakeArrow(24));

        /// <summary>테두리가 또렷한 원 8×8(파편).</summary>
        public static Sprite HardDot => _hardDot != null ? _hardDot : (_hardDot = MakeHardDot(8));

        static Texture2D NewTex(int w, int h, FilterMode filter)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "ProcFx"
            };
            return t;
        }

        static Sprite ToSprite(Texture2D t)
        {
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            s.name = t.name;
            return s;
        }

        static Sprite MakeSoftDot(int size)
        {
            var t = NewTex(size, size, FilterMode.Bilinear);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 가운데 단단한 심 + 바깥 꼬리(가우스 비슷한 감쇠)
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    a = Mathf.Pow(a, 1.6f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, false);
            return ToSprite(t);
        }

        static Sprite MakeHardDot(int size)
        {
            var t = NewTex(size, size, FilterMode.Point);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    bool inside = dx * dx + dy * dy <= r * r;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
                }
            t.SetPixels32(px);
            t.Apply(false, false);
            return ToSprite(t);
        }

        static Sprite MakeStar(int size)
        {
            var t = NewTex(size, size, FilterMode.Point);
            var px = new Color32[size * size];
            int c = size / 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c);
                    // 십자 두 줄기(가운데 굵고 끝으로 가늘게) + 가운데 2×2 심
                    bool arm = (dx == 0 && dy <= c - 1) || (dy == 0 && dx <= c - 1) || (dx <= 1 && dy <= c / 3) || (dy <= 1 && dx <= c / 3);
                    bool core = dx <= 1 && dy <= 1;
                    byte a = (byte)(core ? 255 : arm ? 220 : 0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px);
            t.Apply(false, false);
            return ToSprite(t);
        }

        static Sprite MakeArrow(int size)
        {
            var t = NewTex(size, size, FilterMode.Point);
            var px = new Color32[size * size];
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 오른쪽 끝이 꼭짓점인 삼각형, 왼쪽 가운데를 V자로 파낸다(화살 촉)
                    float fx = x + 0.5f, fy = y + 0.5f - h;
                    float half = (size - fx) * 0.5f;          // 오른쪽으로 갈수록 좁아짐
                    bool inTri = fx >= 2f && Mathf.Abs(fy) <= half;
                    bool notch = fx < 2f + (h - Mathf.Abs(fy)) * 0.6f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(inTri && !notch ? 255 : 0));
                }
            t.SetPixels32(px);
            t.Apply(false, false);
            return ToSprite(t);
        }
    }
}
