using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>안내 한 쪽의 배치(DataSolo/EnhanceTutorial.json pages[n]).</summary>
    public struct EnhanceTutorialPage
    {
        public float FocusX, FocusY, FocusW, FocusH;
        public float CardX, CardY, CardW, CardH;
    }

    /// <summary>DataSolo/EnhanceTutorial.json.</summary>
    public sealed class EnhanceTutorialTuning
    {
        public const string ExtraName = "EnhanceTutorial";
        public readonly List<EnhanceTutorialPage> Pages = new List<EnhanceTutorialPage>();
        public int OddsRuns = 4000, OddsSeed = 1, OddsPercentile = 90, OddsMaxTries = 2000;

        public static EnhanceTutorialTuning From(IContentDb db)
        {
            var t = new EnhanceTutorialTuning();
            JObject jo = db != null ? db.SoloExtra(ExtraName) : null;
            if (jo == null) return t;
            if (jo["pages"] is JArray arr)
                foreach (var tok in arr)
                {
                    var o = tok as JObject;
                    if (o == null) continue;
                    var f = o["focus"] as JObject;
                    var c = o["card"] as JObject;
                    t.Pages.Add(new EnhanceTutorialPage
                    {
                        FocusX = F(f, "x"), FocusY = F(f, "y"), FocusW = F(f, "w"), FocusH = F(f, "h"),
                        CardX = F(c, "x"), CardY = F(c, "y"), CardW = F(c, "w"), CardH = F(c, "h")
                    });
                }
            t.OddsRuns = (int)F(jo, "odds_runs", t.OddsRuns);
            t.OddsSeed = (int)F(jo, "odds_seed", t.OddsSeed);
            t.OddsPercentile = (int)F(jo, "odds_percentile", t.OddsPercentile);
            t.OddsMaxTries = (int)F(jo, "odds_max_tries", t.OddsMaxTries);
            return t;
        }

        static float F(JObject o, string key, float fallback = 0f)
        {
            JToken v = o != null ? o[key] : null;
            return v == null || (v.Type != JTokenType.Integer && v.Type != JTokenType.Float) ? fallback : v.Value<float>();
        }
    }

    /// <summary>한 단계 올리기까지의 시도·골드: 평균과 운 나쁜 쪽(백분위).</summary>
    public struct EnhanceOdds
    {
        public int Stage, Target;
        public double AvgTries, AvgGold;
        public int UnluckyTries;
        public long UnluckyGold;
        public bool Valid;
    }

    /// <summary>
    /// "다음 단계까지 평균 N번 · 운이 나쁜 10%는 M번 이상"(안내 4쪽). 제물·부적 없이 계속 도전한다고 가정한다(08 10.9절 기댓값 표와 같은 가정).
    /// 규칙은 08 10.4절 그대로(데이터 enhance.json): 확률 = min(상한, 기본 + 기운 × 단계), 대성공 = 성공의 일부(+1 도전 제외),
    /// 실패의 일부는 변이(받음 가정, 단계 그대로·기운 +1), 나머지 실패는 하락 가능하면 −1, 아니면 그대로(둘 다 기운 +1).
    /// 하락 가능 = 단계 &gt; 안전 단계 &amp;&amp; 단계 &gt; max(0, 최고 − 바닥 간격). 비용 = 계수 × (단계 + 1)².
    /// 다섯 지팡이가 같은 표를 쓴다(지팡이마다 다른 값이 없다). 고정 씨앗이라 늘 같은 값이 나온다. 결과를 바꾸지 않는 표시용 계산.
    /// </summary>
    public static class EnhanceTutorialOddsCalc
    {
        const int Bp = 10000, Hundred = 100;

        public static EnhanceOdds Compute(EnhanceConfig cfg, int stage, int aura, int floorPeak, EnhanceTutorialTuning t)
        {
            var r = new EnhanceOdds { Stage = stage, Target = stage + 1 };
            if (cfg == null || cfg.BaseProb == null || stage < 0 || stage >= cfg.BaseProb.Count || t == null || t.OddsRuns <= 0) return r;
            var rng = new Random(t.OddsSeed);
            var tries = new int[t.OddsRuns];
            var gold = new long[t.OddsRuns];
            double sumT = 0, sumG = 0;
            for (int run = 0; run < t.OddsRuns; run++)
            {
                int s = stage, a = aura, best = Math.Max(floorPeak, stage), n = 0;
                long g = 0;
                while (s < stage + 1 && n < t.OddsMaxTries)
                {
                    n++;
                    g += (long)cfg.CostCoeff * (s + 1) * (s + 1);
                    int target = s + 1;
                    int pct = target == 1 ? Hundred : Math.Min(cfg.ProbCapPct, cfg.BaseProb[s] + cfg.AuraStepPct * Math.Max(0, Math.Min(a, cfg.AuraMax)));
                    int p = pct * Hundred;
                    int great = target == 1 ? 0 : p * cfg.GreatRatioPct / Hundred;
                    int failAll = Bp - p;
                    int mut = failAll * cfg.MutationRatioPct / Hundred;
                    int roll = rng.Next(Bp);
                    if (roll < great) { s = Math.Min(cfg.MaxStage, s + 2); a = 0; }
                    else if (roll < p) { s = s + 1; a = 0; }
                    else if (roll < p + mut) { a = Math.Min(cfg.AuraMax, a + 1); }
                    else
                    {
                        int floor = Math.Max(0, best - cfg.FloorGap);
                        if (s > cfg.DropSafeMaxStage && s > floor) s--;
                        a = Math.Min(cfg.AuraMax, a + 1);
                    }
                    best = Math.Max(best, s);
                }
                tries[run] = n;
                gold[run] = g;
                sumT += n;
                sumG += g;
            }
            Array.Sort(tries);
            Array.Sort(gold);
            int idx = Math.Min(t.OddsRuns - 1, Math.Max(0, (int)Math.Ceiling(t.OddsRuns * t.OddsPercentile / (double)Hundred) - 1));
            r.AvgTries = sumT / t.OddsRuns;
            r.AvgGold = sumG / t.OddsRuns;
            r.UnluckyTries = tries[idx];
            r.UnluckyGold = gold[idx];
            r.Valid = true;
            return r;
        }
    }
}
