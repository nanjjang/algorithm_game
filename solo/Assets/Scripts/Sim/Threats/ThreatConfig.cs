using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>
    /// 위협 모듈이 쓰는 수치를 콘텐츠에서 한 번 읽어 둔 것. 출처: solo/const.json, waves.json damage, bosses.json common,
    /// characters.json, DataSolo/G05.json. 콘텐츠가 없으면(빈 세계) 전부 0 = "그 규칙 없음"으로 돈다.
    /// </summary>
    public sealed class ThreatConfig
    {
        const float PercentToRatio = 0.01f;     // 단위 환산(% → 비율)
        const float ShrinkUnitsPerCell = 256f;  // 단위 환산(1/256칸 → 칸. S5 3.5절의 정수 격자 단위)

        /// <summary>플레이어 판정원 반지름의 기본값(캐릭터 정의가 없을 때).</summary>
        public float HitRadius;
        public float GrazeRadius;
        /// <summary>R5의 기준 속도 V = 캐릭터 자세 속도의 최솟값(05 요약서 0절).</summary>
        public float EscapeSpeed;
        /// <summary>서리밭의 플레이어 감속(0~1).</summary>
        public float FrostSlowRatio;
        /// <summary>화면 적 탄 상한(0 이하 = 없음).</summary>
        public int EnemyShotCap;
        public int ContactIntervalTicks;

        public int ContactDamage;
        public int TelegraphPlus;
        public int EliteBossPlusFrom;
        public int AllPlusFrom;
        public int AttackCap;
        public int FinalRageDamagePlus;

        public int StarveTicks;
        public int TicketKeepTicks;
        public int FlashLeadTicks;
        public int HazardExpireWarnTicks;

        public float R5CellSize;
        public int R5StepTicks;
        public float R5StepSeconds;
        public int R5ReactionSteps;
        public float R5Shrink;
        public float R5StaticMargin;
        public int R5HorizonExtraSteps;

        public static ThreatConfig From(IContentDb db)
        {
            var c = new ThreatConfig();
            if (db == null) return c;

            var baseline = db.BaselineCharacter;
            if (baseline != null)
            {
                c.HitRadius = baseline.HitRadius;
                c.GrazeRadius = baseline.GrazeRadius;
                c.EscapeSpeed = baseline.StanceSpeed;
            }
            var chars = db.Characters;
            if (chars != null)
            {
                bool first = true;
                for (int i = 0; i < chars.Count; i++)
                {
                    var ch = chars[i];
                    if (ch == null || ch.StanceSpeed <= 0f) continue;
                    if (first || ch.StanceSpeed < c.EscapeSpeed) c.EscapeSpeed = ch.StanceSpeed;
                    first = false;
                }
            }

            var k = db.Const;
            if (k != null)
            {
                c.FrostSlowRatio = k.PlayerSlowOnFrostPercent * PercentToRatio;
                c.EnemyShotCap = k.EnemyShotCap;
                c.ContactIntervalTicks = SimConst.Ticks(k.ContactIntervalSeconds);
            }

            var dmg = db.Waves != null ? db.Waves.Damage : null;
            if (dmg != null)
            {
                c.ContactDamage = dmg.Contact;
                c.TelegraphPlus = dmg.TelegraphPlus;
                c.EliteBossPlusFrom = dmg.EliteBossPlusFrom;
                c.AllPlusFrom = dmg.AllPlusFrom;
                c.AttackCap = dmg.AttackCap;
            }
            if (db.BossCommon != null) c.FinalRageDamagePlus = db.BossCommon.FinalRageDamagePlus;

            JObject x = db.SoloExtra(ThreatSystem.SoloExtraName);
            if (x != null)
            {
                c.StarveTicks = Int(x, "queue", "starve_ticks");
                c.TicketKeepTicks = Int(x, "queue", "ticket_keep_ticks");
                c.FlashLeadTicks = Int(x, "telegraph", "flash_lead_ticks");
                c.HazardExpireWarnTicks = SimConst.Ticks(Float(x, "hazard", "expire_warn_seconds"));
                c.R5CellSize = Float(x, "r5", "cell_size");
                c.R5StepSeconds = Float(x, "r5", "step_seconds");
                c.R5StepTicks = SimConst.Ticks(c.R5StepSeconds);
                c.R5ReactionSteps = Int(x, "r5", "reaction_steps");
                c.R5Shrink = Float(x, "r5", "quantize_shrink_256") / ShrinkUnitsPerCell;
                c.R5StaticMargin = Float(x, "r5", "static_margin");
                c.R5HorizonExtraSteps = Int(x, "r5", "horizon_extra_steps");
            }
            return c;
        }

        static JToken Find(JObject root, string group, string key)
        {
            var g = root[group] as JObject;
            return g != null ? g[key] : null;
        }

        static int Int(JObject root, string group, string key)
        {
            var t = Find(root, group, key);
            return t != null && t.Type != JTokenType.Null ? t.Value<int>() : 0;
        }

        static float Float(JObject root, string group, string key)
        {
            var t = Find(root, group, key);
            return t != null && t.Type != JTokenType.Null ? t.Value<float>() : 0f;
        }
    }
}
