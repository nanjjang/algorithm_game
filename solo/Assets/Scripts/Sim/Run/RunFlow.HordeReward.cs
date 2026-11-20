using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    /// <summary>
    /// 떼 출현 보상 나누기(솔로 조정, DataSolo/G09.json horde.reward_mode = "split", Docs/11-fun-pass.md C절).
    /// 웨이브 디렉터가 잡몹 엔트리를 배율만큼 불리면(사본 = OutsideBudget·엘리트 아님), 원래 한 마리 몫의 골드·점수 재료·체력 조각 확률을
    /// 그 종의 무리 전체(원본 + 사본)에 나눈다: 한 마리 몫 = 명세 몫 × 원본 수 ÷ (원본 + 사본). 소수점은 종마다 누적기로 나른다 →
    /// 처치 순서가 같으면 언제나 같은 결과이고, 다 잡으면 합계가 명세 구성의 합계와 같다.
    /// 구성은 w.Wave.Plan에서 읽는다(Waves 모듈을 직접 부르지 않는다). 떼 출현이 꺼졌거나 사본이 없으면 명세 그대로.
    /// </summary>
    public sealed partial class RunFlow
    {
        const string HordeExtra = "G09", HordeSection = "horde", HordeSplitMode = "split";

        /// <summary>한 웨이브 구성의 나누기 상태(웨이브가 바뀌면 새로 센다).</summary>
        internal sealed class HordeShare
        {
            public WavePlan Plan;
            public readonly Dictionary<string, int> Originals = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Totals = new Dictionary<string, int>();
            public readonly Dictionary<string, long> GoldAcc = new Dictionary<string, long>();
            public readonly Dictionary<string, long> ScoreAcc = new Dictionary<string, long>();
            public readonly Dictionary<string, long> ShardAcc = new Dictionary<string, long>();
            public int OriginalNormals, TotalNormals;
        }

        static bool HordeFlag(IContentDb db, string key, string want)
        {
            var root = db != null ? db.SoloExtra(HordeExtra) : null;
            var h = root != null ? root[HordeSection] as JObject : null;
            if (h == null) return false;
            var en = h["enabled"];
            if (en == null || en.Type != JTokenType.Boolean || !en.Value<bool>()) return false;
            var v = h[key];
            if (v == null) return false;
            if (want == null) return v.Type == JTokenType.Boolean && v.Value<bool>();
            return v.Type == JTokenType.String && v.Value<string>() == want;
        }

        HordeShare ShareState(SimWorld w, RunScratch s)
        {
            var plan = w.Wave != null ? w.Wave.Plan : null;
            if (plan == null) return null;
            var st = s.HordeShare;
            if (st != null && ReferenceEquals(st.Plan, plan)) return st;
            st = new HordeShare { Plan = plan };
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var e = plan.Entries[i];
                if (e.Elite || e.Species == null) continue;
                int n;
                st.Totals.TryGetValue(e.Species, out n);
                st.Totals[e.Species] = n + 1;
                st.TotalNormals++;
                if (e.OutsideBudget) continue;
                st.Originals.TryGetValue(e.Species, out n);
                st.Originals[e.Species] = n + 1;
                st.OriginalNormals++;
            }
            s.HordeShare = st;
            return st;
        }

        /// <summary>그 적 한 마리의 몫(분자/분모). 나누기가 아니면 1/1.</summary>
        void HordeShareOf(SimWorld w, RunScratch s, EnemyState e, out HordeShare st, out int num, out int den)
        {
            st = null; num = den = 1;
            if (e == null || e.IsElite || e.IsBoss || e.SummonerId != 0 || e.DefId == null) return;
            if (!HordeFlag(w.Content, "reward_mode", HordeSplitMode)) return;
            st = ShareState(w, s);
            if (st == null || e.SpawnWave != st.Plan.Wave) { st = null; return; }
            int o, t;
            if (!st.Originals.TryGetValue(e.DefId, out o) || !st.Totals.TryGetValue(e.DefId, out t) || t <= o || o <= 0) { st = null; return; }
            num = o; den = t;
        }

        /// <summary>amount × num ÷ den을 누적기로 나눠 이번에 줄 정수를 돌려준다(남는 몫은 다음 처치로).</summary>
        static int SplitPay(Dictionary<string, long> acc, string key, long amount, int num, int den)
        {
            long carry;
            acc.TryGetValue(key, out carry);
            long total = carry + amount * num;
            acc[key] = total % den;
            return (int)(total / den);
        }

        /// <summary>
        /// 빠른 처리 목표 보정(horde.scale_fast_clear_target): 목표 시간 × (늘린 뒤 잡몹 수 ÷ 명세 잡몹 수). 점수 함수의 식을 바꾸지 않으려고
        /// 걸린 시간에서 늘어난 목표만큼을 뺀 사본으로 점수를 센다(기록되는 통계는 실제 시간 그대로).
        /// </summary>
        WaveStats FastClearStats(SimWorld w, RunScratch s, WaveStats stats)
        {
            if (stats == null || stats.IsBoss || !HordeFlag(w.Content, "scale_fast_clear_target", null)) return stats;
            var st = ShareState(w, s);
            if (st == null || st.OriginalNormals <= 0 || st.TotalNormals <= st.OriginalNormals) return stats;
            var sc = w.Content.Scoring;
            if (sc == null || sc.FastClear == null) return stats;
            long target = (long)sc.FastClear.TargetSeconds * SimConst.TicksPerSecond;
            long extra = target * (st.TotalNormals - st.OriginalNormals) / st.OriginalNormals;
            var copy = new WaveStats
            {
                Wave = stats.Wave, IsBoss = stats.IsBoss, PhantomFirst = stats.PhantomFirst, KillScoreX2 = stats.KillScoreX2,
                BossKilled = stats.BossKilled, ElapsedTicks = (int)(stats.ElapsedTicks - extra), NoHit = stats.NoHit,
                Grazes = stats.Grazes, ElitesKilled = stats.ElitesKilled, KillCount = stats.KillCount
            };
            copy.Whims.AddRange(stats.Whims);
            return copy;
        }
    }
}
