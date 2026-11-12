using System;

namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 표의 "MAN-ui 파일 상태 토큰". 선택(selected)은 바탕이 아니라 K-40 겹그림이라 여기에 없다
    public static class SkinStateToken
    {
        public const string Normal = "normal";
        public const string Hover = "hover";
        public const string Pressed = "pressed";
        public const string On = "on";
        public const string Disabled = "disabled";

        public static string Of(SkinState state)
        {
            switch (state)
            {
                case SkinState.Normal: return Normal;
                case SkinState.Hover: return Hover;
                case SkinState.Pressed: return Pressed;
                case SkinState.On: return On;
                case SkinState.Disabled: return Disabled;
                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}
