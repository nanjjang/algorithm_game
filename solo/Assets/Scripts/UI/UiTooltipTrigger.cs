using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 툴팁 붙이기(UiKit.AttachTooltip). 마우스가 올라오면 TooltipDelay 뒤 툴팁 창(ScreenId.Tooltip)을 띄우고, 나가면 내린다.
    /// Keyboard 모드에서는 UiFocusGroup이 커서 칸의 것을 ShowNow로 띄운다. 글은 부를 때마다 TextProvider에서 받는다(이미 이름 표를 거친 글).
    /// </summary>
    public sealed class UiTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>[임시] 마우스가 머문 뒤 툴팁이 뜨기까지(초)의 기본값. 요약서에 값이 없다. 쓰는 값은 관리자의 Tuning.TooltipDelaySeconds.</summary>
        public const float DefaultDelay = 0.3f;

        public Func<string> TextProvider;
        /// <summary>0 이상이면 이 칸만 이 값으로(음수 = 관리자의 Tuning 값).</summary>
        public float TooltipDelay = -1f;

        bool _inside;
        float _timer;
        bool _shown;

        ScreenManager FindManager()
        {
            var owner = GetComponentInParent<UiCanvasOwner>();
            return owner != null ? owner.Manager : null;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _inside = true;
            _timer = 0f;
        }

        public void OnPointerExit(PointerEventData e)
        {
            _inside = false;
            Hide();
        }

        void Update()
        {
            if (!_inside || _shown) return;
            _timer += Time.unscaledDeltaTime;
            float delay = TooltipDelay;
            if (delay < 0f)
            {
                var m = FindManager();
                delay = m != null ? m.Tuning.TooltipDelaySeconds : DefaultDelay;
            }
            if (_timer >= delay) ShowNow();
        }

        /// <summary>바로 띄운다(커서가 왔을 때).</summary>
        public void ShowNow()
        {
            var m = FindManager();
            string text = null;
            try { text = TextProvider != null ? TextProvider() : null; } catch (Exception ex) { Debug.LogException(ex); }
            if (m == null || string.IsNullOrEmpty(text)) return;
            m.ShowTooltip(text, (RectTransform)transform);
            _shown = true;
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            var m = FindManager();
            if (m != null) m.HideTooltip((RectTransform)transform);
        }

        void OnDisable()
        {
            _inside = false;
            Hide();
        }
    }
}
