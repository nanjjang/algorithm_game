using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    /// <summary>Resources/DataSolo/G06.json의 보충 수치(명세·데이터에 없는 값만). 조각이 없으면 전부 0이다.</summary>
    public sealed class EnemyTuning
    {
        public const string ExtraName = "G06";
        /// <summary>EnemyBuff 이벤트의 버프 이름(북두꺼비 울음). 표시 층이 이 id로 고른다.</summary>
        public const string DrumBuffId = "drum";

        public float DummyHp;
        public float DummyFireIntervalSeconds;
        public float DummyShotSpeed;
        public float DummyShotRadius;
        public int DummyShotDamage;
        public float HeavyFreezeSlow;
        public float CcSlowDefaultSeconds;
        public float BarrageMinGap;
        public float ArriveEpsilon;
        public float QueenDiveTrigger;

        public static EnemyTuning Load(IContentDb db)
        {
            var t = new EnemyTuning();
            JObject j = db != null ? db.SoloExtra(ExtraName) : null;
            if (j == null) return t;
            var d = j["dummy"] as JObject;
            if (d != null)
            {
                t.DummyHp = F(d, "hp");
                t.DummyFireIntervalSeconds = F(d, "fire_interval_seconds");
                t.DummyShotSpeed = F(d, "shot_speed");
                t.DummyShotRadius = F(d, "shot_radius");
                t.DummyShotDamage = (int)F(d, "shot_damage");
            }
            t.HeavyFreezeSlow = F(j, "heavy_freeze_slow");
            t.CcSlowDefaultSeconds = F(j, "cc_slow_default_seconds");
            t.BarrageMinGap = F(j, "slow_barrage_min_gap");
            t.ArriveEpsilon = F(j, "arrive_epsilon");
            t.QueenDiveTrigger = F(j, "queen_dive_trigger");
            return t;
        }

        /// <summary>JObject에서 수 하나(없으면 fallback). 종별 ai·엘리트 params를 읽을 때도 쓴다.</summary>
        public static float F(JObject j, string key, float fallback = 0f)
        {
            if (j == null) return fallback;
            JToken v;
            if (!j.TryGetValue(key, out v) || v == null) return fallback;
            if (v.Type != JTokenType.Float && v.Type != JTokenType.Integer) return fallback;
            return v.Value<float>();
        }

        public static bool B(JObject j, string key)
        {
            if (j == null) return false;
            JToken v;
            return j.TryGetValue(key, out v) && v != null && v.Type == JTokenType.Boolean && v.Value<bool>();
        }
    }
}
