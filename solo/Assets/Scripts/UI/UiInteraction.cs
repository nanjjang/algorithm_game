using System;

namespace Wizard.Solo.UI
{
    /// <summary>누를 수 있는 것의 겉모습 상태(08 요약서 1.3절, 09 요약서 6.1절의 파일 이름 상태 토큰).</summary>
    public enum UiVisualState { Normal = 0, Hover = 1, Pressed = 2, On = 3, Disabled = 4 }

    /// <summary>마지막으로 쓴 입력 장치(08 1.3절 InputModeTracker).</summary>
    public enum UiInputMode { Keyboard = 0, Mouse = 1 }

    /// <summary>선택 괄호 크기(08 1.3절: L = max(w,h); ≤64 Small, 65~240 Medium, 그 위 Large — TV-243).</summary>
    public enum UiBracketSize { Small = 0, Medium = 1, Large = 2 }

    /// <summary>괄호 색: 보통 = 선택 노랑, 꺼짐 = 회청.</summary>
    public enum UiBracketTone { Select = 0, Disabled = 1 }

    /// <summary>
    /// 입력 장치 모드. 키 하나라도 누르면 Keyboard(모든 Hover 해제, 커서 대상에 괄호), 마우스가 움직이면(delta ≠ 0) Mouse
    /// (괄호는 "고른 것"에만). 처음 값은 Keyboard [임시: 키보드만으로도 되는 게임이라 첫 화면에서 커서 괄호가 보이게].
    /// 화면 관리자가 하나를 갖고 모든 화면의 UiFocusGroup이 같이 본다.
    /// </summary>
    public sealed class UiInputModeTracker
    {
        public UiInputModeTracker(UiInputMode initial = UiInputMode.Keyboard) { Mode = initial; }

        public UiInputMode Mode { get; private set; }
        public event Action<UiInputMode> Changed;

        /// <summary>어떤 키든 눌렀을 때. 바뀌었으면 true.</summary>
        public bool OnKeyAny() => Set(UiInputMode.Keyboard);

        /// <summary>마우스가 움직였을 때. 움직임이 0이면 바꾸지 않는다.</summary>
        public bool OnMouseMove(float dx, float dy) => (dx != 0f || dy != 0f) && Set(UiInputMode.Mouse);

        bool Set(UiInputMode m)
        {
            if (Mode == m) return false;
            Mode = m;
            Changed?.Invoke(m);
            return true;
        }
    }

    /// <summary>괄호를 어떻게 그릴지(UiStateRules.Bracket의 결과).</summary>
    public struct UiBracketVisual
    {
        public bool Visible;
        public UiBracketTone Tone;
        /// <summary>커서 괄호는 0.5초 주기로 벌어졌다 오므라든다. 확정(고른 것)은 멈춘다.</summary>
        public bool Pulsing;
        /// <summary>고른 것(확정)이면 오른쪽 위에 확정 체크(K-41).</summary>
        public bool ConfirmCheck;
    }

    /// <summary>
    /// 상태 규약(순수 함수, 08 1.3절·09 6.1절). 모든 버튼·탭·칸·목록 줄·카드가 같은 규칙을 쓴다.
    /// 겉모습 우선순위: !Enabled → Disabled &gt; Pressed &gt; On &gt; Hover(Mouse 모드에서만) &gt; Normal.
    /// </summary>
    public static class UiStateRules
    {
        public static UiVisualState Resolve(bool enabled, bool pressed, bool on, bool hovered, UiInputMode mode)
        {
            if (!enabled) return UiVisualState.Disabled;
            if (pressed) return UiVisualState.Pressed;
            if (on) return UiVisualState.On;
            if (hovered && mode == UiInputMode.Mouse) return UiVisualState.Hover;
            return UiVisualState.Normal;
        }

        /// <summary>Hover 표시 = Mouse 모드 &amp;&amp; 포인터 위(TV-241).</summary>
        public static bool ShowsHover(bool hovered, UiInputMode mode) => hovered && mode == UiInputMode.Mouse;

        /// <summary>
        /// 괄호: Keyboard 모드면 커서 대상 + 고른 것, Mouse 모드면 고른 것에만. 꺼진 것에도 커서는 가고 괄호는 회청(TV-242).
        /// 커서가 있는 것은 고른 것이어도 맥동한다(커서가 우선). 확정 체크는 고른 것에.
        /// </summary>
        public static UiBracketVisual Bracket(bool enabled, bool cursor, bool picked, UiInputMode mode)
        {
            bool cursorShown = cursor && mode == UiInputMode.Keyboard;
            var v = new UiBracketVisual
            {
                Visible = cursorShown || picked,
                Tone = enabled ? UiBracketTone.Select : UiBracketTone.Disabled,
                Pulsing = cursorShown,
                ConfirmCheck = picked
            };
            return v;
        }

        /// <summary>꺼짐 이유 한 줄을 보이는가: 꺼졌고 커서가 있을 때(Keyboard 모드) 또는 마우스가 위에 있을 때(Mouse 모드).</summary>
        public static bool ShowsDisabledReason(bool enabled, bool cursor, bool hovered, UiInputMode mode, bool hasReason)
        {
            if (enabled || !hasReason) return false;
            return mode == UiInputMode.Keyboard ? cursor : hovered;
        }

        /// <summary>파일 이름의 상태 토큰(09 6.1절). Normal은 null(= normal).</summary>
        public static string StateToken(UiVisualState s)
        {
            switch (s)
            {
                case UiVisualState.Hover: return "hover";
                case UiVisualState.Pressed: return "pressed";
                case UiVisualState.On: return "on";
                case UiVisualState.Disabled: return "disabled";
                default: return null;
            }
        }

        /// <summary>그림에 그 상태가 없을 때 대신 쓸 상태(K-25 탭에는 pressed가 없다 → hover, K-26에는 disabled가 없다 → normal).</summary>
        public static UiVisualState Fallback(UiVisualState s)
        {
            switch (s)
            {
                case UiVisualState.Pressed: return UiVisualState.Hover;
                case UiVisualState.On: return UiVisualState.Hover;
                default: return UiVisualState.Normal;
            }
        }
    }

    /// <summary>선택 괄호 치수(08 1.3절, K-40: 선 4px, 바깥 먹색 2px, 대상에서 4px 바깥, 0.5초 주기 2프레임).</summary>
    public static class UiBrackets
    {
        public const int SmallMaxEdgePx = 64, MediumMaxEdgePx = 240;
        public const int SmallArmPx = 12, MediumArmPx = 20, LargeArmPx = 28;
        public const int LineWidthPx = 4, OutlinePx = 2, GapPx = 4;
        /// <summary>맥동: 프레임 0 = 4px 바깥, 프레임 1 = 6px 바깥(K-40 frame_offsets [-4,-4],[-6,-6]).</summary>
        public const int PulseExtraPx = 2;
        public const int PulseFrames = 2;
        /// <summary>한 프레임 = 15틱(0.25초), 한 주기 = 30틱(0.5초).</summary>
        public const int PulseFrameTicks = 15;
        public const float PulseFrameSeconds = PulseFrameTicks / (float)SimConst.TicksPerSecond;

        public static UiBracketSize SizeFor(float widthPx, float heightPx)
        {
            float l = Math.Max(widthPx, heightPx);
            return l <= SmallMaxEdgePx ? UiBracketSize.Small : l <= MediumMaxEdgePx ? UiBracketSize.Medium : UiBracketSize.Large;
        }

        public static int ArmPx(UiBracketSize size)
        {
            switch (size)
            {
                case UiBracketSize.Small: return SmallArmPx;
                case UiBracketSize.Medium: return MediumArmPx;
                default: return LargeArmPx;
            }
        }

        /// <summary>커서가 온 뒤 지난 틱 → 프레임(0, 1). 0~14t = 0, 15~29t = 1, 30t에 다시 0.</summary>
        public static int PulseFrame(long ticksSinceCursorMove)
        {
            if (ticksSinceCursorMove < 0) return 0;
            return (int)(ticksSinceCursorMove / PulseFrameTicks % PulseFrames);
        }

        /// <summary>초 → 프레임(화면 시간으로 돌 때).</summary>
        public static int PulseFrameAt(float seconds) => seconds <= 0f ? 0 : PulseFrame((long)(seconds * SimConst.TicksPerSecond));

        /// <summary>그 프레임에서 괄호가 대상 바깥으로 떨어진 거리(px).</summary>
        public static int GapAt(int frame) => GapPx + (frame == 1 ? PulseExtraPx : 0);
    }
}
