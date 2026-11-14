using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Hints
{
    /// <summary>문맥 힌트 id. 화면 글 열쇠 str.hint.&lt;id&gt;.title / .body.</summary>
    public static class HintIds
    {
        public const string FirstLock = "first_lock", FirstFizzle = "first_fizzle", FirstCrystal = "first_crystal", FirstGraze = "first_graze",
            FirstElite = "first_elite", FirstBoss = "first_boss", FirstLoot = "first_loot", FirstCamp = "first_camp",
            FirstArtUnlock = "first_art_unlock", FirstAffinity = "first_affinity", FirstSettlement = "first_settlement",
            FirstBag = "first_bag", FirstDrop = "first_drop", FirstMutation = "first_mutation",
            StuckDryFire = "stuck_dry_fire", StuckFizzle = "stuck_fizzle";

        /// <summary>처음 한 번만 뜨는 힌트(도감·테스트가 목록으로 돈다).</summary>
        public static readonly string[] FirstTime =
        {
            FirstLock, FirstFizzle, FirstCrystal, FirstGraze, FirstElite, FirstBoss, FirstLoot, FirstCamp, FirstArtUnlock,
            FirstAffinity, FirstSettlement, FirstBag, FirstDrop, FirstMutation
        };

        /// <summary>막혔을 때 뜨는 힌트(판마다 한 번, 프로필에 stuck_max_shows번까지).</summary>
        public static readonly string[] Stuck = { StuckDryFire, StuckFizzle };

        public static bool IsStuck(string id) => id == StuckDryFire || id == StuckFizzle;
        public static string TitleKey(string id) => "str.hint." + id + ".title";
        public static string BodyKey(string id) => "str.hint." + id + ".body";
    }

    /// <summary>DataSolo/Hints.json.</summary>
    public sealed class HintTuning
    {
        public const string ExtraName = "Hints";
        public float CardSeconds = 9f, GapSeconds = 0.6f, EnhanceDelaySeconds = 3.5f, QueueMaxWaitSeconds = 15f;
        public int StuckMaxShows = 3, DryFireCount = 6, FizzleCount = 3;
        public float DryFireWindowSeconds = 4f, FizzleWindowSeconds = 12f;
        public float CardX = 1376f, CardY = 768f, CardW = 528f, CardH = 156f;

        public static HintTuning From(IContentDb db)
        {
            var t = new HintTuning();
            JObject jo = db != null ? db.SoloExtra(ExtraName) : null;
            if (jo == null) return t;
            t.CardSeconds = F(jo, "card_seconds", t.CardSeconds);
            t.GapSeconds = F(jo, "gap_seconds", t.GapSeconds);
            t.EnhanceDelaySeconds = F(jo, "enhance_delay_seconds", t.EnhanceDelaySeconds);
            t.QueueMaxWaitSeconds = F(jo, "queue_max_wait_seconds", t.QueueMaxWaitSeconds);
            t.StuckMaxShows = (int)F(jo, "stuck_max_shows", t.StuckMaxShows);
            t.DryFireCount = (int)F(jo, "dry_fire_count", t.DryFireCount);
            t.FizzleCount = (int)F(jo, "fizzle_count", t.FizzleCount);
            t.DryFireWindowSeconds = F(jo, "dry_fire_window_seconds", t.DryFireWindowSeconds);
            t.FizzleWindowSeconds = F(jo, "fizzle_window_seconds", t.FizzleWindowSeconds);
            var c = jo["card"] as JObject;
            if (c != null)
            {
                t.CardX = F(c, "x", t.CardX); t.CardY = F(c, "y", t.CardY); t.CardW = F(c, "w", t.CardW); t.CardH = F(c, "h", t.CardH);
            }
            return t;
        }

        static float F(JObject jo, string key, float fallback)
        {
            JToken v = jo[key];
            return v == null || (v.Type != JTokenType.Integer && v.Type != JTokenType.Float) ? fallback : v.Value<float>();
        }
    }

    /// <summary>
    /// 본 힌트의 기록(프로필 flags.hints_seen)과 켬/끔(설정 context_hints). 순수 — 저장은 store가 있으면 한다.
    /// 오프라인 프로필 안에만 남는다(서버로 보내지 않는다).
    /// </summary>
    public static class HintBook
    {
        public static bool Enabled(SettingsData s) => s == null || SettingsOptions.Bool(s, OptionKeys.ContextHints);

        public static int Count(ProfileData p, string id)
        {
            if (p == null || p.Flags == null || p.Flags.HintsSeen == null || id == null) return 0;
            int n;
            return p.Flags.HintsSeen.TryGetValue(id, out n) ? n : 0;
        }

        public static bool Seen(ProfileData p, string id) => Count(p, id) > 0;

        /// <summary>지금 띄워도 되나: 처음 힌트는 본 적 없을 때, 막힘 힌트는 stuckMax번 미만일 때.</summary>
        public static bool CanShow(ProfileData p, string id, int stuckMax)
        {
            if (p == null || id == null) return false;
            return HintIds.IsStuck(id) ? Count(p, id) < stuckMax : !Seen(p, id);
        }

        /// <summary>보인 것으로 적는다(+1) 그리고 저장한다.</summary>
        public static void MarkShown(ProfileData p, string id, IProfileStore store)
        {
            if (p == null || p.Flags == null || id == null) return;
            if (p.Flags.HintsSeen == null) p.Flags.HintsSeen = new Dictionary<string, int>();
            p.Flags.HintsSeen[id] = Count(p, id) + 1;
            try { store?.Save(); } catch (System.Exception) { }
        }

        /// <summary>[힌트 다시 보기]: 본 기록을 전부 지운다(튜토리얼·강화 안내 완료 표시는 그대로).</summary>
        public static void ResetAll(ProfileData p, IProfileStore store)
        {
            if (p == null || p.Flags == null) return;
            if (p.Flags.HintsSeen == null) p.Flags.HintsSeen = new Dictionary<string, int>();
            p.Flags.HintsSeen.Clear();
            try { store?.Save(); } catch (System.Exception) { }
        }
    }
}
