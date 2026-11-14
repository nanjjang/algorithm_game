using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>안쪽 테(네 변의 막대). 조심 버튼의 호박색 테, 그림 없이 그리는 올림 테 등에 쓴다. 마우스를 막지 않는다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFrame : MonoBehaviour
    {
        readonly UImage[] _bars = new UImage[4];
        Color _color = Color.white;
        int _thickness = 1;

        public static UiFrame Create(RectTransform parent, Color color, int thicknessPx)
        {
            var go = new GameObject("Frame", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            UiKit.Stretch(rt);
            var f = go.AddComponent<UiFrame>();
            f.Build();
            f.Thickness = thicknessPx;
            f.Color = color;
            return f;
        }

        void Build()
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Edge" + i, typeof(RectTransform), typeof(UImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                _bars[i] = go.GetComponent<UImage>();
                _bars[i].raycastTarget = false;
            }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; for (int i = 0; i < 4; i++) if (_bars[i] != null) _bars[i].color = value; }
        }

        public int Thickness
        {
            get => _thickness;
            set { _thickness = Mathf.Max(1, value); Layout(); }
        }

        void Layout()
        {
            float t = _thickness;
            // 위, 아래, 왼쪽, 오른쪽.
            Set(0, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, t));
            Set(1, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, t));
            Set(2, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(t, 0f));
            Set(3, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(t, 0f));
        }

        void Set(int i, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            if (_bars[i] == null) return;
            var rt = _bars[i].rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
