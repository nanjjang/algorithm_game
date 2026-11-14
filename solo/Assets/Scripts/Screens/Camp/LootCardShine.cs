using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 전리품 카드의 반짝임: 비스듬한 빛 띠가 카드를 가로질러 지나가고 잠깐 쉰다(희귀 이상). 부모에 RectMask2D가 있어 카드 밖으로 새지 않는다.
    /// 좁은 띠 하나만 움직인다(넓은 깜빡임 금지, 08 14.1절). 판 시간이 아니라 화면 시간(unscaled)으로 돈다 — 표시 전용.
    /// </summary>
    public sealed class LootCardShine : MonoBehaviour
    {
        public float Period = 2.6f;
        public float SweepSeconds = 0.7f;
        public float Delay;
        public float Width;
        /// <summary>띠의 가장 밝은 불투명도(글자를 가리지 않게 옅게).</summary>
        public float MaxAlpha = 0.16f;
        UImage _band;
        RectTransform _rt;
        float _t;

        public void Init(UImage band, float cardWidth, float period, float delay)
        {
            _band = band;
            _rt = band.rectTransform;
            Width = cardWidth;
            Period = Mathf.Max(SweepSeconds + 0.1f, period);
            Delay = delay;
            _t = 0f;
            Place(0f);
        }

        void Update()
        {
            if (_band == null) return;
            _t += Time.unscaledDeltaTime;
            float t = _t - Delay;
            if (t < 0f) { Place(0f); return; }
            float k = (t % Period) / SweepSeconds;
            Place(k <= 1f ? k : 0f);
        }

        void Place(float k)
        {
            bool on = k > 0f && k < 1f;
            _band.enabled = on;
            if (!on) return;
            float bw = _rt.sizeDelta.x;
            float x = Mathf.Lerp(-bw * 1.5f, Width + bw * 0.5f, k);
            _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y);
            var c = _band.color;
            c.a = Mathf.Sin(k * Mathf.PI) * MaxAlpha;
            _band.color = c;
        }
    }
}
