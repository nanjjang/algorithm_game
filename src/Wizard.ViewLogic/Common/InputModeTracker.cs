using System;

namespace Wizard.ViewLogic.Common
{
    // spec 13 1.3절 의사코드:
    //   on KeyDown(any)       → lastDevice = Keyboard
    //   on MouseMove(delta≠0) → lastDevice = Mouse
    // 화면 전체가 하나를 같이 쓴다. Hover 를 지우고 괄호를 고치는 일은 InteractableGroup 이 이 값을 읽어 한다.
    // 처음 값은 Keyboard 다 [임시: 명세가 정하지 않았다. 키보드만으로도 되는 게임이라 첫 화면에서 커서 괄호가 보이게 한다].
    public sealed class InputModeTracker
    {
        public InputModeTracker(InputMode initial = InputMode.Keyboard)
        {
            Mode = initial;
        }

        public InputMode Mode { get; private set; }

        public event Action<InputMode>? Changed;

        // 어떤 키든 눌렀을 때. 바뀌었으면 true
        public bool OnKeyAny() => Set(InputMode.Keyboard);

        // 마우스가 움직였을 때. 움직임이 0이면 바꾸지 않는다(spec 13 1.3 "delta≠0")
        public bool OnMouseMove(int dxPx, int dyPx) => (dxPx != 0 || dyPx != 0) && Set(InputMode.Mouse);

        private bool Set(InputMode mode)
        {
            if (Mode == mode)
            {
                return false;
            }
            Mode = mode;
            Changed?.Invoke(mode);
            return true;
        }
    }
}
