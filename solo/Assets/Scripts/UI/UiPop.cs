using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 툭 튀기(배율)와 흔들기(자리) 연출. 자리 흔들림은 LateUpdate에 얹고 다음 Update에 걷어 내므로,
    /// 다른 코드가 그 사이에 자리를 다시 놓아도 어긋나지 않는다. 끝나면 원래 배율·자리로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPop : MonoBehaviour
    {
        float _popT = -1f, _popSeconds, _popScale = 1f;
        Vector3 _restScale = Vector3.one;
        float _shakeT = -1f, _shakeSeconds, _shakePx;
        Vector2 _applied, _lastWritten;
        bool _hasApplied;

        public bool Busy => _popT >= 0f || _shakeT >= 0f;

        public void Pop(float scale, float seconds)
        {
            if (_popT < 0f) _restScale = transform.localScale;
            _popScale = scale;
            _popSeconds = Mathf.Max(0.01f, seconds);
            _popT = 0f;
        }

        public void Shake(float px, float seconds)
        {
            _shakePx = px;
            _shakeSeconds = Mathf.Max(0.01f, seconds);
            _shakeT = 0f;
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
            float dt = Time.unscaledDeltaTime;
            if (_popT >= 0f)
            {
                _popT += dt;
                float k = _popT / _popSeconds;
                if (k >= 1f) { transform.localScale = _restScale; _popT = -1f; }
                else transform.localScale = _restScale * (1f + (_popScale - 1f) * UiMotion.Punch(k));
            }
        }

        void LateUpdate()
        {
            if (_shakeT < 0f) return;
            var rt = transform as RectTransform;
            if (rt == null) { _shakeT = -1f; return; }
            _shakeT += Time.unscaledDeltaTime;
            float k = _shakeT / _shakeSeconds;
            if (k >= 1f) { _shakeT = -1f; return; }
            // 감쇠하는 좌우 흔들림(회전·확대 떨림 없음, 14.1절).
            float amp = _shakePx * (1f - k);
            float x = Mathf.Sin(_shakeT * 2f * Mathf.PI * 18f) * amp;
            _applied = new Vector2(Mathf.Round(x), 0f);
            rt.anchoredPosition += _applied;
            _lastWritten = rt.anchoredPosition;
            _hasApplied = true;
        }

        void OnDisable()
        {
            var rt = transform as RectTransform;
            if (_hasApplied && rt != null && rt.anchoredPosition == _lastWritten) rt.anchoredPosition -= _applied;
            _hasApplied = false;
            _applied = Vector2.zero;
            if (_popT >= 0f) transform.localScale = _restScale;
            _popT = -1f;
            _shakeT = -1f;
        }
    }
}
