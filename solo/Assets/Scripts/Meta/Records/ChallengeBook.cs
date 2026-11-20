using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Records
{
    /// <summary>도전 과제 보상 종류. 전부 선택지·표시 해금이다(결제 없음, 강화 확률·시간에 손대지 않는다).</summary>
    public enum ChallengeRewardKind { None = 0, Title = 1, Cosmetic = 2, LibrarySlot = 3 }

    /// <summary>도전 과제 하나(DataSolo/Challenges.json의 한 줄).</summary>
    public sealed class ChallengeDef
    {
        public string Id;
        /// <summary>1~5 = 그 캐릭터, 0 = 다섯 제자 모두(Aggregate로 합친다).</summary>
        public int Character;
        /// <summary>ChallengeBook.Kind* 중 하나.</summary>
        public string Kind;
        public int Target;
        /// <summary>Character 0일 때: "sum"(다섯의 합) / "min"(다섯 모두 — 가장 낮은 값) / "max"(누구든 하나).</summary>
        public string Aggregate;
        public ChallengeRewardKind Reward;
        public string RewardId;
        public int RewardCount;
    }

    /// <summary>화면이 그릴 진행(값은 Target에서 끊는다).</summary>
    public struct ChallengeProgress
    {
        public ChallengeDef Def;
        public int Value;
        public int Target;
        public bool Done;
        public float Ratio => Target > 0 ? (Value >= Target ? 1f : (float)Value / Target) : (Done ? 1f : 0f);
    }

    /// <summary>
    /// 캐릭터별 도전 과제(엔진 없음, 순수 규칙). 진행 값은 전부 캐릭터 기록(CharacterRecord)에서 계산한다 — 정산이 판 요약으로
    /// 기록을 먼저 갱신하고(AddRunCounters) 그다음 Evaluate가 새로 이룬 과제를 찾아 보상을 넣는다. 한 번 이룬 과제는 되돌아가지 않는다.
    /// 표는 DataSolo/Challenges.json(기획 수치는 JSON에만). 글자는 이름 표 "challenge.&lt;id&gt;"(이름)와 "str.challenge.kind.&lt;kind&gt;"(설명 틀).
    /// </summary>
    public static class ChallengeBook
    {
        public const string ExtraName = "Challenges";
        public const string NamePrefix = "challenge.";

        public const string KindBestWave = "best_wave", KindBestScore = "best_score", KindTotalWaves = "total_waves",
            KindTotalGrazes = "total_grazes", KindTotalKills = "total_kills", KindTotalElites = "total_elites", KindRevenge = "revenge",
            KindRunGrazes = "run_grazes", KindRunNoHitWaves = "run_no_hit_waves", KindBossNoHit = "boss_no_hit";

        public static string NameKey(string id) => NamePrefix + id;

        // ------------------------------------------------------------ 표 읽기

        public static List<ChallengeDef> Load(IContentDb db) => Parse(db != null ? db.SoloExtra(ExtraName) : null);

        public static List<ChallengeDef> Parse(JObject root)
        {
            var list = new List<ChallengeDef>();
            if (root == null || !(root["challenges"] is JArray arr)) return list;
            foreach (JToken t in arr)
            {
                if (!(t is JObject o)) continue;
                var d = new ChallengeDef
                {
                    Id = (string)o["id"],
                    Character = o["character"] != null ? (int)o["character"] : 0,
                    Kind = (string)o["kind"],
                    Target = o["target"] != null ? (int)o["target"] : 0,
                    Aggregate = (string)o["aggregate"] ?? "max",
                };
                if (o["reward"] is JObject r)
                {
                    d.Reward = RewardOf((string)r["kind"]);
                    d.RewardId = (string)r["id"];
                    d.RewardCount = r["count"] != null ? (int)r["count"] : 1;
                }
                if (!string.IsNullOrEmpty(d.Id) && !string.IsNullOrEmpty(d.Kind)) list.Add(d);
            }
            return list;
        }

        static ChallengeRewardKind RewardOf(string s)
        {
            switch (s)
            {
                case "title": return ChallengeRewardKind.Title;
                case "cosmetic": return ChallengeRewardKind.Cosmetic;
                case "library_slot": return ChallengeRewardKind.LibrarySlot;
                default: return ChallengeRewardKind.None;
            }
        }

        /// <summary>그 캐릭터 탭에 보일 과제(character 0이면 "모두" 과제만).</summary>
        public static List<ChallengeDef> For(List<ChallengeDef> all, int character)
        {
            var list = new List<ChallengeDef>();
            if (all == null) return list;
            for (int i = 0; i < all.Count; i++) if (all[i].Character == character) list.Add(all[i]);
            return list;
        }

        // ------------------------------------------------------------ 기록 갱신

        /// <summary>판 요약을 그 캐릭터의 누적 기록에 더한다(정산이 한 번 부른다). 직접 넘긴 웨이브의 결과만 센다.</summary>
        public static void AddRunCounters(CharacterRecord rec, RunSummary s)
        {
            if (rec == null || s == null) return;
            int grazes = 0, kills = 0, elites = 0, noHitRun = 0;
            if (s.WaveResults != null)
                for (int i = 0; i < s.WaveResults.Count; i++)
                {
                    WaveStats st = s.WaveResults[i] != null ? s.WaveResults[i].Stats : null;
                    if (st == null) continue;
                    grazes += st.Grazes;
                    kills += st.KillCount;
                    elites += st.ElitesKilled;
                    if (st.NoHit) noHitRun++;
                    if (st.IsBoss && st.BossKilled && st.NoHit) rec.BossNoHitClears++;
                }
            rec.TotalGrazes += grazes;
            rec.TotalKills += kills > s.TotalKills ? kills : s.TotalKills;
            rec.TotalElites += elites;
            if (grazes > rec.BestRunGrazes) rec.BestRunGrazes = grazes;
            if (noHitRun > rec.BestRunNoHitWaves) rec.BestRunNoHitWaves = noHitRun;
            if (s.KilledPhantomAtWave20) rec.RevengeDone = true;
        }

        // ------------------------------------------------------------ 진행

        /// <summary>한 캐릭터 기록의 그 종류 값.</summary>
        public static int ValueOf(CharacterRecord rec, string kind)
        {
            if (rec == null) return 0;
            switch (kind)
            {
                case KindBestWave: return rec.BestWave;
                case KindBestScore: return rec.BestScore > int.MaxValue ? int.MaxValue : (int)rec.BestScore;
                case KindTotalWaves: return rec.TotalWaves;
                case KindTotalGrazes: return rec.TotalGrazes;
                case KindTotalKills: return rec.TotalKills;
                case KindTotalElites: return rec.TotalElites;
                case KindRevenge: return rec.RevengeDone ? 1 : 0;
                case KindRunGrazes: return rec.BestRunGrazes;
                case KindRunNoHitWaves: return rec.BestRunNoHitWaves;
                case KindBossNoHit: return rec.BossNoHitClears;
                default: return 0;
            }
        }

        public static int ValueOf(ProfileData p, ChallengeDef d)
        {
            if (p == null || d == null) return 0;
            if (d.Character >= 1) return ValueOf(p.Character(d.Character), d.Kind);
            long sum = 0;
            int min = int.MaxValue, max = 0, n = 0;
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                CharacterRecord rec = p.Character(c);
                int v = ValueOf(rec, d.Kind);
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
                n++;
            }
            if (n == 0) return 0;
            switch (d.Aggregate)
            {
                case "sum": return sum > int.MaxValue ? int.MaxValue : (int)sum;
                case "min": return min;
                default: return max;
            }
        }

        public static ChallengeProgress Progress(ProfileData p, ChallengeDef d)
        {
            int v = ValueOf(p, d);
            bool done = p != null && p.Challenges != null && p.Challenges.IsDone(d.Id);
            return new ChallengeProgress { Def = d, Value = v > d.Target ? d.Target : v, Target = d.Target, Done = done || v >= d.Target };
        }

        // ------------------------------------------------------------ 이룸과 보상

        /// <summary>
        /// 아직 이루지 않은 과제 중 지금 기록으로 이룬 것을 찾아 적고 보상을 넣는다(표 순서). 새로 이룬 id를 돌려준다. 저장은 부르는 쪽이 한다.
        /// </summary>
        public static List<string> Evaluate(ProfileData p, List<ChallengeDef> defs, string nowIso)
        {
            var got = new List<string>();
            if (p == null || defs == null) return got;
            if (p.Challenges == null) p.Challenges = new ChallengesData();
            if (p.Challenges.Done == null) p.Challenges.Done = new List<string>();
            if (p.Challenges.DoneAt == null) p.Challenges.DoneAt = new List<string>();
            for (int i = 0; i < defs.Count; i++)
            {
                ChallengeDef d = defs[i];
                if (p.Challenges.IsDone(d.Id) || d.Target <= 0) continue;
                if (ValueOf(p, d) < d.Target) continue;
                p.Challenges.Done.Add(d.Id);
                p.Challenges.DoneAt.Add(nowIso ?? string.Empty);
                Grant(p, d);
                got.Add(d.Id);
            }
            return got;
        }

        static void Grant(ProfileData p, ChallengeDef d)
        {
            switch (d.Reward)
            {
                case ChallengeRewardKind.Title:
                    if (p.Titles == null) p.Titles = new TitlesData();
                    if (!string.IsNullOrEmpty(d.RewardId) && !p.Titles.Owned.Contains(d.RewardId)) p.Titles.Owned.Add(d.RewardId);
                    break;
                case ChallengeRewardKind.Cosmetic:
                    if (p.Cosmetics == null) p.Cosmetics = new CosmeticsData();
                    if (!string.IsNullOrEmpty(d.RewardId) && !p.Cosmetics.Owned.Contains(d.RewardId)) p.Cosmetics.Owned.Add(d.RewardId);
                    break;
                case ChallengeRewardKind.LibrarySlot:
                    p.LibraryExpansions += d.RewardCount > 0 ? d.RewardCount : 1;
                    break;
            }
        }

        /// <summary>그 캐릭터(0 = 모두)의 다음 목표: 아직 이루지 않은 과제 중 진행 비율이 가장 높은 것(같으면 표 순서). 없으면 null.</summary>
        public static ChallengeDef Next(ProfileData p, List<ChallengeDef> defs, int character)
        {
            ChallengeDef best = null;
            float bestRatio = -1f;
            if (defs == null) return null;
            for (int i = 0; i < defs.Count; i++)
            {
                ChallengeDef d = defs[i];
                if (d.Character != character) continue;
                ChallengeProgress pr = Progress(p, d);
                if (pr.Done) continue;
                if (pr.Ratio > bestRatio) { bestRatio = pr.Ratio; best = d; }
            }
            return best;
        }

        public static ChallengeDef Find(List<ChallengeDef> defs, string id)
        {
            if (defs == null) return null;
            for (int i = 0; i < defs.Count; i++) if (defs[i].Id == id) return defs[i];
            return null;
        }
    }
}
