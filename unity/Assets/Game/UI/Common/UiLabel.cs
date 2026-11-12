#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UguiText = UnityEngine.UI.Text;   // Wizard.Text 이름공간과 겹치지 않게
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 글자(자리 표시). 글은 이름 표 열쇠로 받는다(SetKey, SetFormat). 이름 표가 없으면 열쇠를 그대로 찍는다(UiTextSource).
    // 모델이 준 값(숫자, 닉네임)처럼 이름 표에 없는 글만 SetRaw 로 받는다. 숫자는 모델이 UiText.FormatInt 로 만든 것을 넘긴다.
    // 규칙(spec 13 1.4, spec 14 2.5·4.3):
    //   - 글자 크기는 여섯 가지(12/16/24/32/48/64)만. 다른 값은 UiFonts.Get 이 거부한다.
    //   - 넘치면 줄이지 않고 끝을 "…"로 자른다(UiText.Fit). 전체 글은 FullText(툴팁이 쓴다).
    //   - 글자 위치는 정수 px. 비트맵 글꼴(V-12)이 오기 전에는 Unity 내장 글꼴로 찍는다.
    public class UiLabel : UiWidget
    {
        private readonly UiViewContext _context;
        private readonly UguiText _text;
        private string _full = string.Empty;
        private string? _key;

        public UiLabel(UiViewContext context, UiWidget parent, PixelRect localRect, UiFontId font = UiFontId.F16,
            TextAnchor alignment = TextAnchor.MiddleLeft)
            : base("Label", parent, localRect)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Font = UiFonts.Get(font);
            _text = GameObject.AddComponent<UguiText>();
            _text.font = context.Font;
            _text.fontSize = Font.SizePx;
            _text.lineSpacing = 1f;
            _text.alignment = alignment;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.supportRichText = false;
            _text.raycastTarget = false;
            _text.color = UiPalette.Chalk;
            _text.text = string.Empty;
        }

        public UiFontSpec Font { get; }

        // 마지막으로 받은 이름 표 열쇠(SetRaw 면 null)
        public string? Key => _key;

        // 자르기 전의 글
        public string FullText => _full;

        // 화면에 찍힌 글(넘치면 "…"로 자른 것)
        public string ShownText => _text.text;

        public bool IsTruncated => !string.Equals(_full, _text.text, StringComparison.Ordinal);

        public Color32 Color => _text.color;

        public TextAnchor Alignment => _text.alignment;

        public void SetKey(string nameKey)
        {
            _key = nameKey ?? throw new ArgumentNullException(nameof(nameKey));
            Show(_context.Text.Name(nameKey));
        }

        public void SetFormat(string templateKey, IReadOnlyDictionary<string, string>? args)
        {
            _key = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
            Show(_context.Text.Format(templateKey, args));
        }

        // 이름 표 밖의 값(모델이 만든 숫자 글, 닉네임, 대체 그림의 id 글자)
        public void SetRaw(string text)
        {
            _key = null;
            Show(text ?? throw new ArgumentNullException(nameof(text)));
        }

        public void SetColor(Color32 color) => _text.color = color;

        public override void SetRect(PixelRect localRect)
        {
            base.SetRect(localRect);
            if (_text != null)
            {
                Show(_full);
            }
        }

        private void Show(string full)
        {
            _full = full;
            _text.text = UiText.Fit(full, Font.SizePx, LocalRect.Width);
        }
    }
}
