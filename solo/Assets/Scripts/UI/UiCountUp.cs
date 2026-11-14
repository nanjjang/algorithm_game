using System;
using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 글자 칸의 숫자를 0(또는 From)에서 Target까지 차르륵 올린다. 글은 format(값)으로 만든다(이름 표를 거친 숫자 글).
    /// 끝나면 정확히 format(Target). 에디터 모드에서는 Update가 돌지 않으므로 만들 때 바로 끝 값을 써 둔다(Run이 끝 값을 먼저 쓴 뒤 시작).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiCountUp : MonoBehaviour
    {
        UiLabel _label;
        Func<long, string> _format;
        long _from, _target;
        float _t = -1f, _delay, _seconds;
        bool _popAtEnd;

        public long Target => _target;
        public bool Running => _t < 1e8f;

        /// <summary>label에 붙여 delay초 뒤부터 seconds초 동안 from → target. popAtEnd면 끝에 툭 튄다.</summary>
        public static UiCountUp Run(UiLabel label, long target, Func<long, string> format, float delay = 0f,
            float seconds = UiTheme.CountUpSeconds, long from = 0, bool popAtEnd = false)
        {
            if (label == null) return null;
            var c = UiMotion.Ensure<UiCountUp>(label);
            c._label = label;
            c._format = format ?? (v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            c._from = from;
            c._target = target;
            c._delay = Mathf.Max(0f, delay);
            c._seconds = Mathf.Max(0.01f, seconds);
            c._popAtEnd = popAtEnd;
            // 끝 값을 먼저 쓴다(에디터 모드·테스트에서도 글이 맞다). 플레이 중이면 첫 Update가 시작 값으로 바꾼다.
            label.SetText(c._format(target));
            c._t = Application.isPlaying && from != target ? -c._delay : 1e9f;
            if (Application.isPlaying && from != target) label.SetText(c._format(from));
            return c;
        }

        /// <summary>바로 끝 값으로(건너뛰기).</summary>
        public void Complete()
        {
            if (_label != null && _format != null) _label.SetText(_format(_target));
            _t = 1e9f;
        }

        void Update()
        {
            if (_t > 1e8f || _label == null) return;
            _t += Time.unscaledDeltaTime;
            if (_t < 0f) return;
            float k = _t / _seconds;
            if (k >= 1f)
            {
                Complete();
                if (_popAtEnd) UiMotion.Pop(_label, UiTheme.NumberPopScale, UiTheme.NumberPopSeconds);
                return;
            }
            long v = _from + (long)Math.Round((_target - _from) * (double)UiMotion.EaseOutCubic(k));
            _label.SetText(_format(v));
        }

        void OnDisable() { if (_t < 1e8f) Complete(); }
    }
}
