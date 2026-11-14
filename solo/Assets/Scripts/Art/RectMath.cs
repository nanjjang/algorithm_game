using UnityEngine;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// manifest 좌표(왼쪽 위 원점, y 아래로) → Unity Sprite 좌표(왼쪽 아래 원점). 00 문서 10절, 09 요약서 1절.
    /// </summary>
    public static class RectMath
    {
        /// <summary>그림 1칸 = 64px = 1 Unity 단위.</summary>
        public const float PixelsPerUnit = 64f;
        public const int PixelsPerUnitInt = 64;

        /// <summary>시트 높이 TH, 칸 [x, y, w, h] → Rect(x, TH − y − h, w, h).</summary>
        public static Rect ToUnityRect(int x, int y, int w, int h, int sheetHeight)
        {
            return new Rect(x, sheetHeight - y - h, w, h);
        }

        public static Rect ToUnityRect(PixelRect r, int sheetHeight) => ToUnityRect(r.X, r.Y, r.W, r.H, sheetHeight);

        /// <summary>프레임 안 기준점 (px, py) (왼쪽 위 원점) → 정규화 pivot (px / w, (h − py) / h).</summary>
        public static Vector2 ToPivot(int px, int py, int w, int h)
        {
            if (w <= 0 || h <= 0) return new Vector2(0.5f, 0.5f);
            return new Vector2((float)px / w, (float)(h - py) / h);
        }

        public static Vector2 ToPivot(PixelPoint p, int w, int h) => ToPivot(p.X, p.Y, w, h);

        /// <summary>9조각 원본 [l, t, r, b] → Unity Sprite border (L, B, R, T). 값이 없으면 0.</summary>
        public static Vector4 LtrbToUnityBorder(int[] ltrb)
        {
            if (ltrb == null || ltrb.Length < 4) return Vector4.zero;
            return new Vector4(ltrb[0], ltrb[3], ltrb[2], ltrb[1]);
        }

        /// <summary>rect가 시트 안에 들어가는가(Sprite.Create는 밖이면 예외를 던진다 → 대체 그림).</summary>
        public static bool Fits(PixelRect r, int sheetWidth, int sheetHeight)
        {
            return r.W > 0 && r.H > 0 && r.X >= 0 && r.Y >= 0 && r.X + r.W <= sheetWidth && r.Y + r.H <= sheetHeight;
        }

        /// <summary>행 우선 격자 칸 번호 i → rect((i % columns)·w, (i / columns)·h, w, h).</summary>
        public static PixelRect GridCell(int index, int columns, int cellW, int cellH)
        {
            if (columns < 1) columns = 1;
            return new PixelRect((index % columns) * cellW, (index / columns) * cellH, cellW, cellH);
        }

        /// <summary>가로 띠의 k번째 프레임(UI manifest: width·height는 프레임 하나).</summary>
        public static PixelRect StripFrame(int k, int frameW, int frameH) => new PixelRect(k * frameW, 0, frameW, frameH);
    }
}
