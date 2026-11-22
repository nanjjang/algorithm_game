using UnityEngine;
using Wizard.Solo.Art;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 글자 한 칸(UnityEngine.UI.Text + 글꼴 크기 6종). 한 줄 칸은 넘치면 끝을 "…"로 자르고(줄이지 않는다, 08 1.4절)
    /// 잘렸을 때만 전체 글을 툴팁으로 붙인다. 여러 줄 칸(Wrap)은 자르지 않고 칸 안에서 줄을 바꾼다.
    /// 폭은 UiTextFit 규칙(한글·기호 = 글자 크기, 숫자·영문 = 절반)으로 센다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiLabel : MonoBehaviour
    {
        UText _text;
        string _full = string.Empty;
        UiFontId _size = UiFontId.F16;
        bool _wrap;
        bool _truncated;
        float _fitWidth = -1f;
        UiTooltipTrigger _tip;
        /// <summary>Refit을 한 번이라도 했는가(같은 글 건너뛰기는 그 뒤에만).</summary>
        bool _fitted;
        System.Func<string> _tipProvider;

        public UText Text => _text != null ? _text : (_text = GetComponent<UText>());
        public string FullText => _full;
        public UiFontId Size => _size;
        public bool Truncated => _truncated;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>UiKit.Label이 부른다.</summary>
        public void Init(UText text, UiFontId size, bool wrap)
        {
            _text = text;
            _size = size;
            _wrap = wrap;
            _fitted = false;
        }

        public bool Wrap
        {
            get => _wrap;
            set { _wrap = value; Refit(); }
        }

        public Color Color
        {
            get => Text != null ? Text.color : Color.white;
            set { if (Text != null) Text.color = value; }
        }

        /// <summary>
        /// 글을 바꾼다(이미 이름 표를 거친 글). 이미 맞춘 글과 같고 칸 폭도 그대로면 아무것도 하지 않는다
        /// (매 프레임 같은 글을 넣는 HUD·화면이 자르기 계산·글 메시 재생성을 되풀이하지 않게).
        /// </summary>
        public void SetText(string s)
        {
            s = s ?? string.Empty;
            if (_fitted && string.Equals(s, _full) && (_wrap || Mathf.Abs(Rect.rect.width - _fitWidth) <= 0.5f)) return;
            _full = s;
            Refit();
        }

        void OnRectTransformDimensionsChange()
        {
            if (Text == null) return;
            float w = Rect.rect.width;
            if (Mathf.Abs(w - _fitWidth) > 0.5f) Refit();
        }

        /// <summary>칸 폭에 맞춰 다시 자른다.</summary>
        public void Refit()
        {
            var t = Text;
            if (t == null) return;
            _fitted = true;
            if (_wrap)
            {
                _truncated = false;
                t.text = _full;
            }
            else
            {
                int px = (int)_size;
                _fitWidth = Rect.rect.width;
                int max = Mathf.FloorToInt(_fitWidth);
                _truncated = max > 0 && UiTextFit.IsTruncated(_full, px, max);
                t.text = max > 0 ? UiTextFit.Fit(_full, px, max) : _full;
            }
            UpdateTooltip();
        }

        void UpdateTooltip()
        {
            if (_truncated)
            {
                if (_tip == null) _tip = gameObject.GetComponent<UiTooltipTrigger>();
                if (_tip == null) _tip = gameObject.AddComponent<UiTooltipTrigger>();
                _tip.TextProvider = _tipProvider ?? (_tipProvider = () => _full);
                _tip.enabled = true;
                if (Text != null) Text.raycastTarget = true;
            }
            else if (_tip != null)
            {
                _tip.enabled = false;
                if (Text != null) Text.raycastTarget = false;
            }
        }
    }
}
