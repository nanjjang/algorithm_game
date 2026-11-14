using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Solo.UI
{
    public enum UiTriangleDir { Up = 0, Down = 1, Left = 2, Right = 3 }

    /// <summary>
    /// 코드로 그리는 삼각형(그림 없이 도는 도형). 조심 버튼의 경고 삼각(호박색), 슬라이더 ◀▶의 대체 그림.
    /// InnerMark가 참이면 위 방향 삼각형 가운데에 먹색 느낌표 막대와 점을 그린다(경고 삼각).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiTriangle : MaskableGraphic
    {
        [SerializeField] UiTriangleDir _dir = UiTriangleDir.Up;
        [SerializeField] bool _innerMark;
        public Color MarkColor = UiTheme.Ink;

        public UiTriangleDir Direction { get => _dir; set { _dir = value; SetVerticesDirty(); } }
        public bool InnerMark { get => _innerMark; set { _innerMark = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 a, b, c;
            switch (_dir)
            {
                case UiTriangleDir.Down:
                    a = new Vector2(r.xMin, r.yMax); b = new Vector2(r.xMax, r.yMax); c = new Vector2(r.center.x, r.yMin); break;
                case UiTriangleDir.Left:
                    a = new Vector2(r.xMax, r.yMax); b = new Vector2(r.xMax, r.yMin); c = new Vector2(r.xMin, r.center.y); break;
                case UiTriangleDir.Right:
                    a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMin, r.yMax); c = new Vector2(r.xMax, r.center.y); break;
                default:
                    a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMax, r.yMin); c = new Vector2(r.center.x, r.yMax); break;
            }
            AddTri(vh, a, b, c, color);
            if (_innerMark && _dir == UiTriangleDir.Up)
            {
                float w = Mathf.Max(2f, Mathf.Round(r.width / 8f));
                float cx = r.center.x;
                float top = r.yMin + r.height * 0.70f, bottom = r.yMin + r.height * 0.38f;
                AddQuad(vh, new Rect(cx - w / 2f, bottom, w, top - bottom), MarkColor);
                AddQuad(vh, new Rect(cx - w / 2f, r.yMin + r.height * 0.14f, w, w), MarkColor);
            }
        }

        static void AddTri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        static void AddQuad(VertexHelper vh, Rect q, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(q.xMin, q.yMin), col, Vector2.zero);
            vh.AddVert(new Vector2(q.xMin, q.yMax), col, Vector2.zero);
            vh.AddVert(new Vector2(q.xMax, q.yMax), col, Vector2.zero);
            vh.AddVert(new Vector2(q.xMax, q.yMin), col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
