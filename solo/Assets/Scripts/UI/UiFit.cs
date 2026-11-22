using System;
using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>바탕을 창 끝까지 넓힐 가장자리(위 띠는 Left|Right|Top, 아래 띠는 Left|Right|Bottom, 바탕은 All).</summary>
    [Flags]
    public enum UiBleedEdges
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        Horizontal = Left | Right,
        Vertical = Top | Bottom,
        All = Left | Right | Top | Bottom
    }

    /// <summary>
    /// 창 맞춤 계산(순수 함수, 엔진 호출 없음 — Rect·Vector2는 값 형식만 쓴다). 내용은 1920×1080 안전 영역(UiScale)에 그대로 두고,
    /// 바탕만 창(캔버스) 끝까지 넓힌다: 단색·9조각 판은 가장자리를 캔버스 끝으로 늘리고(Bleed), 그림은 비율을 지키며 덮고 가운데에서 자른다(Cover).
    /// Rect는 y 위가 +인 부모 local 좌표(RectTransform과 같다).
    /// </summary>
    public static class UiFit
    {
        /// <summary>창 px를 그 배율로 나눈 캔버스 크기(기준 단위). 배율이 0 이하면 1로 본다.</summary>
        public static Vector2 CanvasSize(int screenWidth, int screenHeight, float scale)
        {
            float s = scale > 0f ? scale : 1f;
            return new Vector2(Mathf.Max(0, screenWidth) / s, Mathf.Max(0, screenHeight) / s);
        }

        /// <summary>1920×1080 안전 영역 밖의 한쪽 여백(가로, 세로 — 캔버스 단위). 창이 기준보다 좁은 축은 0.</summary>
        public static Vector2 SafeMargin(Vector2 canvasSize)
        {
            return new Vector2(Mathf.Max(0f, (canvasSize.x - UiScale.RefWidth) * 0.5f),
                Mathf.Max(0f, (canvasSize.y - UiScale.RefHeight) * 0.5f));
        }

        /// <summary>
        /// authored(만든 자리)의 edges 쪽 가장자리를 canvas 끝까지 넓힌다. 줄이지는 않는다(창이 더 좁으면 만든 자리 그대로) —
        /// 안전 영역은 늘 덮인다.
        /// </summary>
        public static Rect Bleed(Rect authored, Rect canvas, UiBleedEdges edges)
        {
            float xMin = authored.xMin, xMax = authored.xMax, yMin = authored.yMin, yMax = authored.yMax;
            if ((edges & UiBleedEdges.Left) != 0) xMin = Mathf.Min(xMin, canvas.xMin);
            if ((edges & UiBleedEdges.Right) != 0) xMax = Mathf.Max(xMax, canvas.xMax);
            if ((edges & UiBleedEdges.Bottom) != 0) yMin = Mathf.Min(yMin, canvas.yMin);
            if ((edges & UiBleedEdges.Top) != 0) yMax = Mathf.Max(yMax, canvas.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// rect를 크기 그대로 pin 쪽 canvas 가장자리에 붙게 옮긴다(화면 테두리 번쩍임·모서리 글). 마주 보는 두 쪽을 다 고르면 그 축은 가운데 그대로.
        /// </summary>
        public static Rect Pin(Rect rect, Rect canvas, UiBleedEdges pin)
        {
            bool l = (pin & UiBleedEdges.Left) != 0, r = (pin & UiBleedEdges.Right) != 0;
            bool t = (pin & UiBleedEdges.Top) != 0, b = (pin & UiBleedEdges.Bottom) != 0;
            if (l && !r) rect.x += canvas.xMin - rect.xMin;
            else if (r && !l) rect.x += canvas.xMax - rect.xMax;
            if (t && !b) rect.y += canvas.yMax - rect.yMax;
            else if (b && !t) rect.y += canvas.yMin - rect.yMin;
            return rect;
        }

        /// <summary>비율 aspect(너비/높이)의 그림이 box를 빈틈없이 덮는 가장 작은 크기(남는 쪽은 잘린다). aspect가 0 이하면 box 그대로.</summary>
        public static Vector2 CoverSize(Vector2 box, float aspect)
        {
            if (aspect <= 0f || float.IsNaN(aspect) || box.x <= 0f || box.y <= 0f) return box;
            float w = box.x, h = w / aspect;
            if (h < box.y) { h = box.y; w = h * aspect; }
            return new Vector2(w, h);
        }

        /// <summary>비율 aspect의 그림이 box 안에 다 들어가는 가장 큰 크기(남는 쪽은 빈다). aspect가 0 이하면 box 그대로.</summary>
        public static Vector2 ContainSize(Vector2 box, float aspect)
        {
            if (aspect <= 0f || float.IsNaN(aspect) || box.x <= 0f || box.y <= 0f) return box;
            float w = box.x, h = w / aspect;
            if (h > box.y) { h = box.y; w = h * aspect; }
            return new Vector2(w, h);
        }

        /// <summary>box를 덮는(CoverSize) 사각형을 box 가운데에 둔다.</summary>
        public static Rect Cover(Rect box, float aspect)
        {
            Vector2 s = CoverSize(box.size, aspect);
            return new Rect(box.center - s * 0.5f, s);
        }

        /// <summary>box 안에 다 들어가는(ContainSize) 사각형을 box 가운데에 둔다.</summary>
        public static Rect Contain(Rect box, float aspect)
        {
            Vector2 s = ContainSize(box.size, aspect);
            return new Rect(box.center - s * 0.5f, s);
        }

        /// <summary>
        /// 장면 통째(그림 + 그 위에 얹은 모닥불·캐릭터) 덮기: authored를 focal 점(authored 안의 절대 좌표)을 축으로 균일하게 키워
        /// canvas를 빈틈없이 덮게 하는 가장 작은 배율(1 이상). focal이 가운데면 보통 덮기와 같고, 아래쪽에 두면 아래 장면이 덜 잘린다.
        /// </summary>
        public static float CoverScaleAbout(Rect authored, Rect canvas, Vector2 focal)
        {
            float s = 1f;
            s = Mathf.Max(s, Need(focal.x - canvas.xMin, focal.x - authored.xMin));
            s = Mathf.Max(s, Need(canvas.xMax - focal.x, authored.xMax - focal.x));
            s = Mathf.Max(s, Need(focal.y - canvas.yMin, focal.y - authored.yMin));
            s = Mathf.Max(s, Need(canvas.yMax - focal.y, authored.yMax - focal.y));
            return s;
        }

        static float Need(float want, float have)
        {
            if (want <= 0f) return 1f;
            if (have <= 1e-4f) return 1f; // 축이 가장자리에 붙어 있으면 그쪽은 키워도 못 덮는다 — 다른 쪽에 맡긴다
            return want / have;
        }

        /// <summary>
        /// 보이는 사각형(화면 px viewW×viewH, 1칸 = ppu px)이 content(칸) 밖을 보이지 않으려면 ppu에 곱해야 할 최소 배율(1 미만이면 이미 덮는다).
        /// 전장 카메라가 넓은 창·보스 줌 아웃에서 전장 밖 빈 곳(먹색)을 보이지 않게 할 때 쓴다.
        /// </summary>
        public static float MinZoomToCover(float viewW, float viewH, float ppu, float contentW, float contentH)
        {
            if (viewW <= 0f || viewH <= 0f || ppu <= 0f || contentW <= 0f || contentH <= 0f) return 0f;
            return Mathf.Max(viewW / (contentW * ppu), viewH / (contentH * ppu));
        }
    }
}
