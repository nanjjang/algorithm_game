using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 시작 묶음 화면의 수치. 기본값은 08 요약서의 값 그대로이고, 수치 조각 `Resources/DataSolo/G18.json`의 `start` 절이
    /// 있으면 그 값으로 덮는다(00 문서 9절 수치 조각 규칙. 조각 파일은 이 작업의 소유 경로 밖이라 아직 없다 — 결과 보고에 적음).
    /// </summary>
    public sealed class StartTuning
    {
        /// <summary>타이틀 시작 안내의 밝기 주기(초). 08 3.2절 "2초(120 t) 주기로 밝기만".</summary>
        public float TitlePulseSeconds = 2f;
        /// <summary>시작 안내의 가장 어두운 밝기(0~1). 원문에 값이 없다 — 솔로 임시.</summary>
        public float TitlePulseMinBrightness = 0.5f;
        /// <summary>메인 메뉴 배경 시간대(기기 시각의 시). 08 4.1절 [임시](U-14): 06:00~16:59 낮, 17:00~19:59 해질녘, 20:00~05:59 밤.</summary>
        public int DayStartHour = 6, DuskStartHour = 17, NightStartHour = 20;
        /// <summary>메인 메뉴가 기기 시계를 다시 보는 간격(초). 시간대 바뀜 확인용(표시 갱신 주기, 기획 수치 아님).</summary>
        public float ClockPollSeconds = 1f;

        public static StartTuning Load(IContentDb db)
        {
            var t = new StartTuning();
            JObject extra = null;
            try { extra = db != null ? db.SoloExtra("G18") : null; }
            catch (System.Exception) { extra = null; }
            var s = extra != null ? extra["start"] as JObject : null;
            if (s == null) return t;
            t.TitlePulseSeconds = Float(s, "title_pulse_seconds", t.TitlePulseSeconds);
            t.TitlePulseMinBrightness = Float(s, "title_pulse_min_brightness", t.TitlePulseMinBrightness);
            t.DayStartHour = Int(s, "day_start_hour", t.DayStartHour);
            t.DuskStartHour = Int(s, "dusk_start_hour", t.DuskStartHour);
            t.NightStartHour = Int(s, "night_start_hour", t.NightStartHour);
            return t;
        }

        static float Float(JObject o, string key, float fallback)
        {
            var v = o[key];
            return v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer) ? (float)v : fallback;
        }

        static int Int(JObject o, string key, int fallback)
        {
            var v = o[key];
            return v != null && v.Type == JTokenType.Integer ? (int)v : fallback;
        }
    }
}
