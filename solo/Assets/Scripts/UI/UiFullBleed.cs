using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>UiFullBleed가 창에 맞추는 방법.</summary>
    public enum UiFitMode
    {
        /// <summary>Edges 쪽 가장자리를 창 끝까지 늘인다(단색·9조각 판·덮개·띠).</summary>
        Fill,
        /// <summary>창 전체를 비율대로 덮고 가운데에서 자른다(배경 그림 한 장).</summary>
        Cover,
        /// <summary>창 안에 비율대로 다 들어가게 가장 크게(영상 — 잘리면 안 되는 것). 남는 곳은 뒤 바탕이 채운다.</summary>
        Contain,
        /// <summary>크기는 그대로 두고 Focal을 축으로 균일하게 키워 창을 덮는다(그림 위에 모닥불·캐릭터를 얹은 장면 통째).</summary>
        ScaleCover
    }

    /// <summary>
    /// 바탕 넓히기(반응형). 캔버스 루트는 1920×1080 안전 영역(가운데, UiScale 배율)이고 창이 16:9가 아니거나 정수 배율이 남기면
    /// 둘레에 여백이 생긴다. 이것을 붙인 RectTransform은 만든 자리를 기억해 두었다가 Mode대로 창(루트 캔버스)에 맞춘다(계산은 UiFit).
    /// 창 크기·배율이 바뀌거나 부모가 움직이면(화면 들어오는 미끄럼) LateUpdate에서 다시 맞춘다 — 화면을 다시 만들 필요가 없다.
    /// 바탕·덮개·띠 그림에만 붙인다(안의 글·단추는 안전 영역 그대로). 회전·뒤집기 없는 칸 전제. 캔버스가 없으면(편집 모드 시험) 만든 자리 그대로.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFullBleed : MonoBehaviour
    {
        public UiFitMode Mode = UiFitMode.Fill;
        /// <summary>Fill에서 넓힐 가장자리.</summary>
        public UiBleedEdges Edges = UiBleedEdges.All;
        /// <summary>Fill에서 크기 그대로 창 가장자리로 옮겨 붙일 쪽(넓힌 뒤에 적용). 화면 테두리 띠·모서리 글.</summary>
        public UiBleedEdges Pin = UiBleedEdges.None;
        /// <summary>Cover·Contain의 그림 비율(너비/높이). 0 이하면 Fill처럼.</summary>
        public float Aspect;
        /// <summary>ScaleCover의 축(만든 자리 안의 비율, 왼쪽 위 (0,0) ~ 오른쪽 아래 (1,1)).</summary>
        public Vector2 Focal = new Vector2(0.5f, 0.5f);

        static readonly Vector3[] Corners = new Vector3[4];

        RectTransform _rt;
        RectTransform _canvasRt;
        Rect _authored;
        bool _captured;
        Vector2 _lastCanvasSize, _lastParentSize;
        Matrix4x4 _lastParentMatrix;

        /// <summary>만든 자리(부모 local, y 위 +).</summary>
        public Rect Authored => _authored;

        /// <summary>rt를 창 끝까지 늘인다(edges 쪽만), pin 쪽은 크기 그대로 창 가장자리로 옮긴다. 이미 붙어 있으면 설정만 바꾼다.</summary>
        public static UiFullBleed Fill(RectTransform rt, UiBleedEdges edges = UiBleedEdges.All, UiBleedEdges pin = UiBleedEdges.None)
        {
            var b = Attach(rt);
            if (b == null) return null;
            b.Mode = UiFitMode.Fill;
            b.Edges = edges;
            b.Pin = pin;
            b.Refit();
            return b;
        }

        /// <summary>rt가 창 전체를 aspect 비율로 덮게 한다(가운데 정렬, 남는 쪽은 잘림).</summary>
        public static UiFullBleed Cover(RectTransform rt, float aspect) => Set(rt, UiFitMode.Cover, aspect);

        /// <summary>rt가 창 안에 aspect 비율로 가장 크게 들어가게 한다(가운데 정렬).</summary>
        public static UiFullBleed Contain(RectTransform rt, float aspect) => Set(rt, UiFitMode.Contain, aspect);

        /// <summary>그림 한 장을 창 전체 덮기로(스프라이트 비율). 스프라이트가 없으면 늘이기. preserveAspect는 끈다(크기가 이미 비율대로다).</summary>
        public static UiFullBleed Cover(UImage img)
        {
            if (img == null) return null;
            float aspect = SpriteAspect(img.sprite);
            img.preserveAspect = false;
            return aspect > 0f ? Cover(img.rectTransform, aspect) : Fill(img.rectTransform);
        }

        /// <summary>
        /// 장면 통째(rt와 그 아이들)를 focal(만든 자리 안 비율, 왼쪽 위 원점)을 축으로 균일하게 키워 창을 덮는다.
        /// 그림 위에 정해진 px 자리로 얹은 것(모닥불·캐릭터)이 그림과 어긋나지 않는다.
        /// </summary>
        public static UiFullBleed ScaleCover(RectTransform rt, Vector2 focal)
        {
            var b = Attach(rt);
            if (b == null) return null;
            b.Mode = UiFitMode.ScaleCover;
            b.Focal = new Vector2(Mathf.Clamp01(focal.x), Mathf.Clamp01(focal.y));
            b.Refit();
            return b;
        }

        /// <summary>rt의 가로를 부모 너비에 붙인다(늘어나는 부모 안의 줄·띠 조각). 세로 자리·높이는 그대로.</summary>
        public static void StretchX(RectTransform rt)
        {
            if (rt == null) return;
            float h = rt.sizeDelta.y, y = rt.anchoredPosition.y;
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
            rt.sizeDelta = new Vector2(0f, h);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>스프라이트 비율(너비/높이). 없으면 0.</summary>
        public static float SpriteAspect(Sprite s)
        {
            if (s == null) return 0f;
            var r = s.rect;
            return r.height > 0f ? r.width / r.height : 0f;
        }

        static UiFullBleed Set(RectTransform rt, UiFitMode mode, float aspect)
        {
            var b = Attach(rt);
            if (b == null) return null;
            b.Mode = mode;
            b.Aspect = aspect;
            b.Refit();
            return b;
        }

        static UiFullBleed Attach(RectTransform rt)
        {
            if (rt == null) return null;
            var b = rt.GetComponent<UiFullBleed>();
            if (b == null) b = rt.gameObject.AddComponent<UiFullBleed>();
            b.Capture();
            return b;
        }

        void Capture()
        {
            if (_captured) return;
            _rt = (RectTransform)transform;
            // 만든 자리를 부모 local 사각형으로(회전·크기 없는 칸 전제).
            Rect r = _rt.rect;
            Vector2 lp = _rt.localPosition;
            _authored = new Rect(lp + r.min, r.size);
            _captured = true;
        }

        void OnEnable() { if (_captured) Refit(); }

        void LateUpdate()
        {
            if (!_captured) return;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            if (_canvasRt == null) FindCanvas();
            if (CanvasSize() == _lastCanvasSize && parent.rect.size == _lastParentSize && parent.localToWorldMatrix == _lastParentMatrix) return;
            Refit();
        }

        Vector2 CanvasSize()
        {
            if (_canvasRt == null) return Vector2.zero;
            Vector3 ls = _canvasRt.lossyScale;
            Vector2 s = _canvasRt.rect.size;
            return new Vector2(s.x * ls.x, s.y * ls.y);
        }

        void FindCanvas()
        {
            var c = GetComponentInParent<Canvas>(true);
            if (c != null) c = c.rootCanvas;
            _canvasRt = c != null ? c.transform as RectTransform : null;
        }

        /// <summary>지금 창에 맞춰 다시 놓는다(보통은 LateUpdate가 알아서 부른다).</summary>
        public void Refit()
        {
            if (!_captured) return;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            if (_canvasRt == null) FindCanvas();
            Rect canvas = _authored;
            if (_canvasRt != null)
            {
                _canvasRt.GetWorldCorners(Corners);
                Vector2 a = parent.InverseTransformPoint(Corners[0]);
                Vector2 c = parent.InverseTransformPoint(Corners[2]);
                canvas = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
            }
            _lastCanvasSize = CanvasSize();
            _lastParentSize = parent.rect.size;
            _lastParentMatrix = parent.localToWorldMatrix;

            switch (Mode)
            {
                case UiFitMode.ScaleCover:
                {
                    // 축 = 만든 자리 안의 Focal(왼쪽 위 원점 → y 위 +로 바꾼다).
                    var pivot = new Vector2(Focal.x, 1f - Focal.y);
                    Vector2 f = _authored.min + Vector2.Scale(_authored.size, pivot);
                    // 배율은 창 크기로만 정한다(만든 자리 가운데에 창을 둔다 — 쉴 때 안전 영역은 창 가운데). 지금 자리로 재면
                    // 화면 들어오는 미끄럼 동안 배율이 출렁인다.
                    var rest = new Rect(_authored.center - canvas.size * 0.5f, canvas.size);
                    float s = UiFit.CoverScaleAbout(_authored, rest, f);
                    _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
                    _rt.pivot = pivot;
                    _rt.sizeDelta = _authored.size;
                    _rt.anchoredPosition = f - parent.rect.center;
                    _rt.localScale = new Vector3(s, s, 1f);
                    return;
                }
                case UiFitMode.Cover when Aspect > 0f:
                    // 창만 덮는다(만든 자리와 합치지 않는다 — 미끄럼 동안 합친 칸이 커져 그림이 출렁이지 않게). 캔버스가 없으면 canvas = 만든 자리.
                    Apply(parent, UiFit.Cover(canvas, Aspect));
                    return;
                case UiFitMode.Contain when Aspect > 0f:
                    Apply(parent, UiFit.Contain(canvas, Aspect));
                    return;
                case UiFitMode.Fill:
                    Apply(parent, UiFit.Pin(UiFit.Bleed(_authored, canvas, Edges), canvas, Pin));
                    return;
                default:
                    Apply(parent, UiFit.Bleed(_authored, canvas, UiBleedEdges.All));
                    return;
            }
        }

        void Apply(RectTransform parent, Rect r)
        {
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = r.size;
            _rt.anchoredPosition = r.center - parent.rect.center;
        }
    }
}
