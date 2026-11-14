using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 보스 공통 뼈대의 보충 수치(Resources/DataSolo/G07.json). bosses.json에 없는 값만 둔다.
    /// 필드의 처음 값은 조각을 읽지 못했을 때(가짜 콘텐츠) 쓰는 값이고 조각과 같다(테스트가 둘을 맞춰 본다).
    /// </summary>
    public sealed class BossTuning
    {
        public const string ExtraName = "G07";

        /// <summary>복수 대상 허상이 나오는 웨이브(05 요약서 10.3절).</summary>
        public int RevengeWave = 20;
        public float SpawnXRatio = 0.5f;
        public float SpawnYRatioA = 0.25f;
        public float SpawnYRatioB = 0.75f;
        /// <summary>되비침의 만개 연출 틱(판정 없음. 05 요약서 10.2절).</summary>
        public int[] ReflectionBloomTicks = { 24, 54, 84 };
        public int CollapseMasterLineTicks = 72;
        public float R1ReactionSeconds = 0.25f;
        public float R1MarginSeconds = 0.05f;
        public float R3CheckDistance = 3f;
        public float R3MinGap = 1f;
        public int BoulderShardDamage = 1;
        public int EmberDamage = 1;
        public float ChaseStopDistance = 0.1f;
        public float KeepDistanceBand = 0.25f;
        public float SummonRingRadius = 1.5f;
        public float EmberSegmentLength = 1f;
        public float CopyHitSlack = 0.1f;
        public int PatternStallTicks = 600;

        public static BossTuning Load(IContentDb db)
        {
            var t = new BossTuning();
            JObject j = db != null ? db.SoloExtra(ExtraName) : null;
            if (j == null) return t;
            t.RevengeWave = Int(j, "revenge_wave", t.RevengeWave);
            t.SpawnXRatio = Float(j, "spawn_x_ratio", t.SpawnXRatio);
            t.SpawnYRatioA = Float(j, "spawn_y_ratio_a", t.SpawnYRatioA);
            t.SpawnYRatioB = Float(j, "spawn_y_ratio_b", t.SpawnYRatioB);
            var arr = j["reflection_bloom_ticks"] as JArray;
            if (arr != null)
            {
                var ticks = new int[arr.Count];
                for (int i = 0; i < ticks.Length; i++) ticks[i] = (int)arr[i];
                t.ReflectionBloomTicks = ticks;
            }
            t.CollapseMasterLineTicks = Int(j, "collapse_master_line_ticks", t.CollapseMasterLineTicks);
            t.R1ReactionSeconds = Float(j, "r1_reaction_seconds", t.R1ReactionSeconds);
            t.R1MarginSeconds = Float(j, "r1_margin_seconds", t.R1MarginSeconds);
            t.R3CheckDistance = Float(j, "r3_check_distance", t.R3CheckDistance);
            t.R3MinGap = Float(j, "r3_min_gap", t.R3MinGap);
            t.BoulderShardDamage = Int(j, "boulder_shard_damage", t.BoulderShardDamage);
            t.EmberDamage = Int(j, "ember_damage", t.EmberDamage);
            t.ChaseStopDistance = Float(j, "chase_stop_distance", t.ChaseStopDistance);
            t.KeepDistanceBand = Float(j, "keep_distance_band", t.KeepDistanceBand);
            t.SummonRingRadius = Float(j, "summon_ring_radius", t.SummonRingRadius);
            t.EmberSegmentLength = Float(j, "ember_segment_length", t.EmberSegmentLength);
            t.CopyHitSlack = Float(j, "copy_hit_slack", t.CopyHitSlack);
            t.PatternStallTicks = Int(j, "pattern_stall_ticks", t.PatternStallTicks);
            return t;
        }

        internal static int Int(JObject j, string key, int fallback)
        {
            var v = j != null ? j[key] : null;
            return v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (int)v : fallback;
        }

        internal static float Float(JObject j, string key, float fallback)
        {
            var v = j != null ? j[key] : null;
            return v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (float)v : fallback;
        }

        internal static bool Bool(JObject j, string key, bool fallback)
        {
            var v = j != null ? j[key] : null;
            return v != null && v.Type == JTokenType.Boolean ? (bool)v : fallback;
        }

        internal static string Str(JObject j, string key, string fallback)
        {
            var v = j != null ? j[key] : null;
            return v != null && v.Type == JTokenType.String ? (string)v : fallback;
        }
    }
}
