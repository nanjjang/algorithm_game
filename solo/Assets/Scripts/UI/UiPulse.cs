using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 그림 하나의 불투명도를 느리게 숨쉬게 한다(쓸 수 있음 반짝임, 선택 강조). 작은 면적에만 쓴다(넓은 깜빡임 금지, 08 14.1절).
    /// 색(RGB)은 그대로 두고 알파만 MinAlpha~MaxAlpha 사이를 Period초 주기로 오간다. 꺼지면 MaxAlpha로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class UiPulse : MonoBehaviour
    {
        public float MinAlpha = 0.25f, MaxAlpha = 0.9f, Period = UiTheme.ReadyPulsePeriod;
        Graphic _g;
        float _t;

        public static UiPulse Attach(Graphic g, float minAlpha, float maxAlpha, float period = UiTheme.ReadyPulsePeriod)
        {
            if (g == null) return null;
            var p = UiMotion.Ensure<UiPulse>(g);
            p.MinAlpha = minAlpha;
            p.MaxAlpha = maxAlpha;
            p.Period = Mathf.Max(0.3f, period);
            p.Set(maxAlpha);
            return p;
        }

        void Awake() { _g = GetComponent<Graphic>(); }

        void OnEnable() { _t = 0f; }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = 0.5f - 0.5f * Mathf.Cos(_t * 2f * Mathf.PI / Mathf.Max(0.3f, Period));
            Set(Mathf.Lerp(MaxAlpha, MinAlpha, k));
        }

        void Set(float a)
        {
            if (_g == null) _g = GetComponent<Graphic>();
            if (_g == null) return;
            var c = _g.color;
            c.a = a;
            _g.color = c;
        }

        void OnDisable() => Set(MaxAlpha);
    }
}
