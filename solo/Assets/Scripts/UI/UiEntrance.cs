using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 켜질 때마다(OnEnable) 투명 → 불투명, SlideFrom px만큼 떨어진 곳 → 제자리, FromScale → 원래 배율로 들어오는 등장 연출.
    /// 화면 전환(ScreenManager)과 판·카드 등장에 쓴다. 입력은 막지 않는다(CanvasGroup.blocksRaycasts는 건드리지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiEntrance : MonoBehaviour
    {
        public float Delay;
        public float Seconds = UiTheme.PanelPopSeconds;
        /// <summary>시작 자리(제자리에서 px, 오른쪽 +, 위 +).</summary>
        public Vector2 SlideFrom;
        public float FromScale = 1f;

        CanvasGroup _group;
        float _t = 1e9f;
        float _restAlpha = 1f;
        Vector3 _restScale = Vector3.one;
        Vector2 _applied, _lastWritten;
        bool _hasApplied;

        public bool Playing => _t < 9e8f && isActiveAndEnabled;

        void OnEnable()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _restAlpha = 1f;
            _restScale = Vector3.one;
            _t = -Mathf.Max(0f, Delay);
            Apply(0f);
        }

        /// <summary>처음부터 다시 한다.</summary>
        public void Replay()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            _t = -Mathf.Max(0f, Delay);
            Apply(0f);
        }

        void Update()
        {
            var rt = transform as RectTransform;
            if (_hasApplied && rt != null)
            {
                if (rt.anchoredPosition == _lastWritten) rt.anchoredPosition -= _applied;
                _hasApplied = false;
                _applied = Vector2.zero;
            }
            if (_t > 9e8f) return;
            _t += Time.unscaledDeltaTime;
        }

        void LateUpdate()
        {
            if (_t > 9e8f) return;
            float k = _t <= 0f ? 0f : _t / Mathf.Max(0.01f, Seconds);
            if (k >= 1f) { Finish(); return; }
            Apply(k);
        }

        void Apply(float k)
        {
            float e = UiMotion.EaseOutCubic(k);
            if (_group != null) _group.alpha = _restAlpha * e;
            float s = Mathf.LerpUnclamped(FromScale, 1f, FromScale < 1f ? UiMotion.EaseOutBack(k) : e);
            transform.localScale = _restScale * s;
            var rt = transform as RectTransform;
            if (rt != null && SlideFrom != Vector2.zero)
            {
                if (_hasApplied && rt.anchoredPosition == _lastWritten) rt.anchoredPosition -= _applied;
                _applied = new Vector2(Mathf.Round(SlideFrom.x * (1f - e)), Mathf.Round(SlideFrom.y * (1f - e)));
                rt.anchoredPosition += _applied;
                _lastWritten = rt.anchoredPosition;
                _hasApplied = true;
            }
        }

        void Finish()
        {
            if (_group != null) _group.alpha = _restAlpha;
            transform.localScale = _restScale;
            var rt = transform as RectTransform;
            if (_hasApplied && rt != null && rt.anchoredPosition == _lastWritten) rt.anchoredPosition -= _applied;
            _hasApplied = false;
            _applied = Vector2.zero;
            _t = 1e9f;
        }

        void OnDisable() => Finish();
    }
}
