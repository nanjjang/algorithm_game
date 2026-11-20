using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 섞는 규칙의 수치(10 요약서 5.2절 = E 19.5). 기본값은 요약서 값 그대로다.
    /// 보충 수치 조각 Resources/DataSolo/F4.json(ctx.Content.SoloExtra("F4"))에 같은 열쇠가 있으면 그 값을 쓴다.
    /// </summary>
    public sealed class SoundMixConfig
    {
        public const string ExtraName = "F4";
        public const string ExtraSection = "sound_mix";

        /// <summary>전체 동시 재생 수.</summary>
        public int MaxVoices = 32;
        /// <summary>적중 1초 횟수.</summary>
        public int HitPerSecond = 8;
        /// <summary>처치 1초 횟수.</summary>
        public int KillPerSecond = 6;
        /// <summary>예고 음 동시 수.</summary>
        public int TelegraphConcurrent = 2;
        /// <summary>피격 때 다른 소리 누르기(dB, 음수)와 그 길이(초).</summary>
        public float DuckDb = -6f;
        public double DuckSeconds = 0.4;
        /// <summary>어떤 소리도 평소보다 이만큼(dB) 넘게 크지 않다(+13 종 포함).</summary>
        public float MaxBoostDb = 6f;
        /// <summary>
        /// 소리 하나가 보이스를 차지하는 길이(초). 소리 파일이 없어 길이를 모르므로 솔로 임시 1.0초(원문에 값 없음).
        /// 클립이 생기면 재생기가 클립 길이를 넘긴다.
        /// </summary>
        public double DefaultVoiceSeconds = 1.0;
        /// <summary>초당 횟수를 세는 창(초).</summary>
        public double RateWindowSeconds = 1.0;

        public static SoundMixConfig Spec() => new SoundMixConfig();

        /// <summary>보충 조각(최상위 또는 "sound_mix" 절)에서 읽는다. null이면 요약서 값.</summary>
        public static SoundMixConfig FromJson(JObject extra)
        {
            var c = new SoundMixConfig();
            if (extra == null) return c;
            var o = extra[ExtraSection] as JObject ?? extra;
            c.MaxVoices = Int(o, "max_voices", c.MaxVoices);
            c.HitPerSecond = Int(o, "hit_per_second", c.HitPerSecond);
            c.KillPerSecond = Int(o, "kill_per_second", c.KillPerSecond);
            c.TelegraphConcurrent = Int(o, "telegraph_concurrent", c.TelegraphConcurrent);
            c.DuckDb = Float(o, "duck_db", c.DuckDb);
            c.DuckSeconds = Double(o, "duck_seconds", c.DuckSeconds);
            c.MaxBoostDb = Float(o, "max_boost_db", c.MaxBoostDb);
            c.DefaultVoiceSeconds = Double(o, "default_voice_seconds", c.DefaultVoiceSeconds);
            c.RateWindowSeconds = Double(o, "rate_window_seconds", c.RateWindowSeconds);
            return c;
        }

        static int Int(JObject o, string key, int def)
        {
            var t = o[key];
            return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (int)(double)t : def;
        }

        static double Double(JObject o, string key, double def)
        {
            var t = o[key];
            return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (double)t : def;
        }

        static float Float(JObject o, string key, float def)
        {
            var t = o[key];
            return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (float)(double)t : def;
        }
    }
}
