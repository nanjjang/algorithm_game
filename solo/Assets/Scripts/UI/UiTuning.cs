using Newtonsoft.Json.Linq;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// UI 기반의 솔로 임시 수치(요약서에 값이 없는 것). 정본은 보충 수치 조각 Resources/DataSolo/F3.json의 "ui" 절이고
    /// (ctx.Content.SoloExtra("F3")), 조각이 없거나 열쇠가 빠지면 아래 기본값을 쓴다(테스트·콘텐츠 없는 부팅).
    /// 화면 관리자가 하나 만들어 들고(ScreenManager.Tuning), 위젯은 자기 캔버스의 관리자에서 읽는다(정적 가변 상태 없음).
    /// </summary>
    public sealed class UiTuning
    {
        public const string ExtraName = "F3";
        public const string ExtraSection = "ui";

        /// <summary>메뉴 키 반복: 누르고 있으면 이 시간 뒤부터(초).</summary>
        public float KeyRepeatDelaySeconds = MenuInput.DefaultRepeatDelay;
        /// <summary>메뉴 키 반복 간격(초).</summary>
        public float KeyRepeatIntervalSeconds = MenuInput.DefaultRepeatInterval;
        /// <summary>마우스가 머문 뒤 툴팁이 뜨기까지(초).</summary>
        public float TooltipDelaySeconds = UiTooltipTrigger.DefaultDelay;
        /// <summary>키보드로 눌렀을 때 누름 모습을 보이는 시간(초).</summary>
        public float PressFlashSeconds = UiWidget.PressFlashSeconds;
        /// <summary>꺼진 것을 눌렀을 때 이유를 다시 보여 주는 시간(초, 08 1.3절 U-15).</summary>
        public float DisabledReasonSeconds = UiWidget.ReasonFlashSeconds;
        /// <summary>토스트가 내려오는 시간(초).</summary>
        public float ToastSlideSeconds = ScreenManager.ToastSlideSeconds;

        public static UiTuning Defaults() => new UiTuning();

        /// <summary>보충 조각(최상위 또는 "ui" 절)에서 읽는다. null이면 기본값.</summary>
        public static UiTuning FromJson(JObject extra)
        {
            var t = new UiTuning();
            if (extra == null) return t;
            var o = extra[ExtraSection] as JObject ?? extra;
            t.KeyRepeatDelaySeconds = Float(o, "key_repeat_delay_seconds", t.KeyRepeatDelaySeconds);
            t.KeyRepeatIntervalSeconds = Float(o, "key_repeat_interval_seconds", t.KeyRepeatIntervalSeconds);
            t.TooltipDelaySeconds = Float(o, "tooltip_delay_seconds", t.TooltipDelaySeconds);
            t.PressFlashSeconds = Float(o, "press_flash_seconds", t.PressFlashSeconds);
            t.DisabledReasonSeconds = Float(o, "disabled_reason_seconds", t.DisabledReasonSeconds);
            t.ToastSlideSeconds = Float(o, "toast_slide_seconds", t.ToastSlideSeconds);
            return t;
        }

        static float Float(JObject o, string key, float def)
        {
            var v = o[key];
            return v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (float)(double)v : def;
        }
    }
}
