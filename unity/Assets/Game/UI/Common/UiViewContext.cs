#nullable enable
using System;
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 화면 뷰들이 같이 쓰는 것: 글자 출처, 입력 모드(화면 전체가 하나, spec 13 1.3), 그릴 무대, 글꼴.
    // 전역 싱글턴을 두지 않는다. 조립하는 쪽(CompositionRoot, 테스트)이 만들어 ScreenHost 에 준다.
    public sealed class UiViewContext
    {
        // 비트맵 글꼴(F-12·F-16, V-12)이 들어오기 전의 자리 표시 글꼴. Unity 에 들어 있는 것을 쓴다
        public const string PlaceholderFontName = "LegacyRuntime.ttf";

        public UiViewContext(UiTextSource text, InputModeTracker mode, RectTransform stage, Font? font = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            Stage = stage != null ? stage : throw new ArgumentNullException(nameof(stage));
            Font = font != null ? font : Resources.GetBuiltinResource<Font>(PlaceholderFontName);
        }

        public UiTextSource Text { get; }

        public InputModeTracker Mode { get; }

        // 1920×1080 무대(UiCanvas.Stage). 화면 뷰의 뿌리가 여기에 붙는다
        public RectTransform Stage { get; }

        public Font Font { get; }
    }
}
