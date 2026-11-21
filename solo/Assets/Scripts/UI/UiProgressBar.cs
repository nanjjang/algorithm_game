using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>진행 막대(K-36 rail 9s5 / fill 9s4). Value01 = 0~1. 누를 수 없다. 채움 색은 FillColor(기본 분필 흰색, 그림이 있으면 흰색 곱).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiProgressBar : MonoBehaviour
    {
        public UImage Rail;
        public UImage Fill;
        float _value;

        public float Value01
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp01(value);
                if (Fill == null) return;
                var rt = Fill.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(_value, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                Fill.enabled = _value > 0f;
            }
        }

        public Color FillColor
        {
            get => Fill != null ? Fill.color : Color.white;
            set { if (Fill != null) Fill.color = value; }
        }
    }
}
