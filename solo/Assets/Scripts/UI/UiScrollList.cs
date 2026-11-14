using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 세로 목록(ScrollRect + RectMask2D + K-34 스크롤 막대). 줄은 AddRow(높이)로 위에서부터 쌓고, 내용 높이는 저절로 맞춘다.
    /// 마우스 휠·끌기로 움직이고, 키보드 커서가 옮겨 가면 화면이 EnsureVisible(줄)을 불러 보이게 한다.
    /// UiFocusGroup.CursorChanged에 EnsureVisible을 걸어 두면 된다(UiKit.ScrollList의 followFocus).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiScrollList : MonoBehaviour
    {
        public ScrollRect Scroll;
        public RectTransform Viewport;
        public RectTransform Content;
        public RectTransform ScrollRail;
        public RectTransform ScrollThumb;
        /// <summary>줄 사이 간격(px).</summary>
        public float Gap = 4f;
        readonly List<RectTransform> _rows = new List<RectTransform>();
        float _height;

        public IReadOnlyList<RectTransform> Rows => _rows;
        public float ContentHeight => _height;

        /// <summary>줄 하나를 더한다(폭 = 목록 폭, 높이 = height). 그 줄의 RectTransform(왼쪽 위 원점 Place 좌표계)을 준다.</summary>
        public RectTransform AddRow(float height, string name = "Row")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Content, false);
            float y = _rows.Count == 0 ? 0f : _height + Gap;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, -(y + height));
            rt.offsetMax = new Vector2(0f, -y);
            _rows.Add(rt);
            _height = y + height;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, _height);
            return rt;
        }

        /// <summary>줄을 모두 지운다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _rows.Count; i++) if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();
            _height = 0f;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, 0f);
            Content.anchoredPosition = Vector2.zero;
        }

        /// <summary>목록 안의 것(줄이나 그 자식)이 보이도록 굴린다.</summary>
        public void EnsureVisible(RectTransform target)
        {
            if (target == null || Content == null || Viewport == null) return;
            if (!target.IsChildOf(Content)) return;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 topW = corners[1], botW = corners[0];
            float top = -Content.InverseTransformPoint(topW).y;     // 내용 위에서 아래로 px
            float bottom = -Content.InverseTransformPoint(botW).y;
            float viewH = Viewport.rect.height;
            float y = Content.anchoredPosition.y;                    // 지금 굴린 양(위로 올라간 px)
            const float Margin = 12f;                                 // 괄호가 잘리지 않게
            if (top - Margin < y) y = top - Margin;
            else if (bottom + Margin > y + viewH) y = bottom + Margin - viewH;
            float max = Mathf.Max(0f, _height - viewH);
            y = Mathf.Clamp(y, 0f, max);
            Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, y);
            if (Scroll != null) Scroll.velocity = Vector2.zero;
        }

        void LateUpdate()
        {
            if (ScrollRail == null || ScrollThumb == null || Viewport == null) return;
            float viewH = Viewport.rect.height;
            bool need = _height > viewH + 0.5f;
            ScrollRail.gameObject.SetActive(need);
            if (!need) return;
            float railH = ScrollRail.rect.height;
            float thumbH = Mathf.Max(24f, railH * viewH / _height);
            float max = Mathf.Max(1f, _height - viewH);
            float t = Mathf.Clamp01(Content.anchoredPosition.y / max);
            ScrollThumb.sizeDelta = new Vector2(ScrollThumb.sizeDelta.x, thumbH);
            ScrollThumb.anchoredPosition = new Vector2(0f, -t * (railH - thumbH));
        }

        internal static void SetupImage(UImage img, Color c) { img.color = c; img.raycastTarget = false; }
    }
}
