namespace Wizard.Solo.UI
{
    /// <summary>
    /// 공용 UI 소리 사건 id(data/presentation/event_bindings.json의 ui.common.*). 화면은 ctx.Sound.Play(id, SoundBus.Ui)로 낸다.
    /// 화면 고유 사건(ui.camp.*, ui.enhance.* …)은 그 화면이 bindings 파일의 열쇠 그대로 쓴다.
    /// </summary>
    public static class UiSounds
    {
        public const string ConfirmCheck = "ui.common.confirm_check";
        public const string CursorBracketPulse = "ui.common.cursor_bracket_pulse";
        public const string CursorMove = "ui.common.cursor_move";
        public const string DisabledPress = "ui.common.disabled_press";
        public const string HoverEnter = "ui.common.hover_enter";
        public const string HoverExit = "ui.common.hover_exit";
        public const string InputError = "ui.common.input_error";
        public const string Press = "ui.common.press";
        public const string Release = "ui.common.release";
        public const string ScreenClose = "ui.common.screen_close";
        public const string ScreenOpen = "ui.common.screen_open";
        public const string ToggleOff = "ui.common.toggle_off";
        public const string ToggleOn = "ui.common.toggle_on";
        public const string TooltipShow = "ui.common.tooltip_show";

        /// <summary>ISoundPlayer가 null이어도 되는 도우미.</summary>
        public static void Play(ISoundPlayer sound, string id)
        {
            if (sound != null && !string.IsNullOrEmpty(id)) sound.Play(id, SoundBus.Ui);
        }
    }
}
