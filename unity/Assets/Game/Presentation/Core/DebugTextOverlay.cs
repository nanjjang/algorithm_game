#nullable enable
using UnityEngine;

namespace Wizard.Unity.Presentation.Core
{
    // 화면 왼쪽 위의 디버그 글자(틱, 개체 수, 섬 간격 밀리초 등). IMGUI 로 찍어 글꼴 에셋이 필요 없다.
    // 글자는 WorldPresenter 가 바뀔 때만 다시 만든다. 화면 글자의 이름 표(M6-TXT-01)가 생기기 전이라 숫자와 ASCII 약자만 쓴다 [임시].
    public sealed class DebugTextOverlay : MonoBehaviour
    {
        private const float MarginPx = 8f;                // [임시]
        private const int FontSizePx = 14;                // [임시]

        private GUIStyle? _style;
        private string _text = string.Empty;

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        private void OnGUI()
        {
            if (_text.Length == 0)
            {
                return;
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = FontSizePx, wordWrap = false };
                _style.normal.textColor = ShapePalette.DebugText;
            }
            GUI.Label(new Rect(MarginPx, MarginPx, Screen.width - MarginPx * 2f, Screen.height - MarginPx * 2f), _text, _style);
        }
    }
}
