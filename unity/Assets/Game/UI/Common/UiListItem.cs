#nullable enable
using System;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 목록 한 줄의 자료. 글은 이름 표 열쇠(TextKey, Key 로 만든다) 또는 모델이 만든 값(RawText, Literal 로 만든다) 가운데 하나다.
    // 만드는 함수 이름을 Raw 로 하지 않는다: unity/Assets 에서는 점 뒤의 낱말 Raw 를 Fix64 원시값 접근으로 보고 막는다(FixConvertSearchTests).
    // Control 은 모델이 쥔 누를 수 있는 것이다. 모델이 쥐지 않으면 목록이 Id 로 새로 만들어 화면 묶음에 넣는다.
    public readonly struct UiListItem
    {
        public readonly string Id;
        public readonly string? TextKey;
        public readonly string? RawText;
        public readonly Interactable? Control;

        private UiListItem(string id, string? textKey, string? rawText, Interactable? control)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TextKey = textKey;
            RawText = rawText;
            Control = control;
        }

        public static UiListItem Key(string id, string textKey, Interactable? control = null) =>
            new UiListItem(id, textKey ?? throw new ArgumentNullException(nameof(textKey)), null, control);

        public static UiListItem Literal(string id, string literalText, Interactable? control = null) =>
            new UiListItem(id, null, literalText ?? throw new ArgumentNullException(nameof(literalText)), control);
    }
}
