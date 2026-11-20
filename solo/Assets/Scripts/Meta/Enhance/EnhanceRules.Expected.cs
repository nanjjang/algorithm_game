using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>기댓값 풀이의 가정(07 요약서 6절). 기본값 = 화면의 기댓값 표(제물·부적 없음, 변이는 받는다).</summary>
    public struct ExpectedOptions
    {
        /// <summary>모든 시도에 더하는 제물 보너스(%p). 표시는 0.</summary>
        public int SacrificePct;
        /// <summary>이 단계 이상에서 하락할 시도마다 부적을 쓴다(하락만 막고 기운은 안 쌓인다).</summary>
        public bool UseCharm;
        public int CharmFromStage;
        /// <summary>하락 바닥이 없다고 본다(실패마다 −1. 하락 없는 낮은 단계는 그대로). 회귀 비교용.</summary>
        public bool NoFloor;
    }

    public struct ExpectedValue
    {
        public double Attempts;
        public double Gold;
        public double StonesLow, StonesMid, StonesHigh;
        public double Charms;
    }

    public static partial class EnhanceRules
    {
        // 풀이의 열(오른쪽 벡터) 번호.
        const int ColAttempts = 0, ColGold = 1, ColLow = 2, ColMid = 3, ColHigh = 4, ColCharm = 5, ColCount = 6;

        /// <summary>기댓값 상태의 셋째 성분: g = min(floor_gap, floor_peak − stage).</summary>
        public static int FloorGapState(EnhanceConfig cfg, int stage, int floorPeak) =>
            Math.Max(0, Math.Min(cfg.FloorGap, floorPeak - stage));

        /// <summary>기댓값 상태의 수 = (단계 0..최고) × (기운 0..최대) × (바닥과의 차 0..floor_gap).</summary>
        public static int ExpectedStateCount(EnhanceConfig cfg) =>
            (cfg.MaxStage + 1) * (cfg.AuraMax + 1) * (cfg.FloorGap + 1);

        struct Step
        {
            public double PGreat, PSuccess, PMutation, PFail;
            public int GreatStage, GreatGap;
            public int SuccessStage, SuccessGap;
            public int AuraUp;
            /// <summary>일반 실패 뒤의 상태.</summary>
            public int FailStage, FailAura, FailGap;
            public bool FailUsesCharm;
        }

        static Step StepOf(EnhanceConfig cfg, int s, int a, int g, ExpectedOptions opt)
        {
            double p = SuccessPct(cfg, s, a, opt.SacrificePct) / (double)PctFull;
            double pg = s == 0 ? 0 : p * cfg.GreatRatioPct / PctFull;
            double f = 1 - p;
            double pm = f * cfg.MutationRatioPct / PctFull;
            var st = new Step
            {
                PGreat = pg, PSuccess = p - pg, PMutation = pm, PFail = f - pm,
                GreatStage = StageAfterGreat(cfg, s),
                SuccessStage = StageAfterSuccess(cfg, s),
                AuraUp = Math.Min(cfg.AuraMax, a + 1),
            };
            st.GreatGap = Math.Max(0, g - (st.GreatStage - s));
            st.SuccessGap = Math.Max(0, g - (st.SuccessStage - s));

            bool drop = s > cfg.DropSafeMaxStage && (opt.NoFloor || g < cfg.FloorGap);
            if (drop && opt.UseCharm && s >= opt.CharmFromStage)
            {
                st.FailUsesCharm = true;
                st.FailStage = s; st.FailAura = a; st.FailGap = g;
            }
            else if (drop)
            {
                st.FailStage = s - 1; st.FailAura = st.AuraUp; st.FailGap = Math.Min(cfg.FloorGap, g + 1);
            }
            else
            {
                st.FailStage = s; st.FailAura = st.AuraUp; st.FailGap = g;
            }
            return st;
        }

        /// <summary>
        /// (단계, 기운, floor_peak)에서 목표 단계에 처음 닿기까지의 평균 시도·골드·강화석(07 요약서 6절).
        /// E[x] = c(x) + Σ P(x→y) E[y] 를 가우스 소거로 정확히 푼다. 목표 이상이면 전부 0.
        /// </summary>
        public static ExpectedValue Expected(EnhanceConfig cfg, int stage, int aura, int floorPeak, int target, ExpectedOptions opt)
        {
            int t = Math.Min(target, cfg.MaxStage);
            if (stage >= t || stage < 0) return new ExpectedValue();

            int auraN = cfg.AuraMax + 1, gapN = cfg.FloorGap + 1;
            int n = t * auraN * gapN;
            var m = new double[n, n + ColCount];

            for (int s = 0; s < t; s++)
                for (int a = 0; a < auraN; a++)
                    for (int g = 0; g < gapN; g++)
                    {
                        int row = (s * auraN + a) * gapN + g;
                        Step st = StepOf(cfg, s, a, g, opt);
                        m[row, row] += 1;
                        Subtract(m, row, st.PGreat, st.GreatStage, 0, st.GreatGap, t, auraN, gapN);
                        Subtract(m, row, st.PSuccess, st.SuccessStage, 0, st.SuccessGap, t, auraN, gapN);
                        Subtract(m, row, st.PMutation, s, st.AuraUp, g, t, auraN, gapN);
                        Subtract(m, row, st.PFail, st.FailStage, st.FailAura, st.FailGap, t, auraN, gapN);

                        m[row, n + ColAttempts] = 1;
                        m[row, n + ColGold] = Cost(cfg, s);
                        m[row, n + ColLow + (int)StoneTierFor(cfg, s)] = cfg.StonesPerAttempt;
                        m[row, n + ColCharm] = st.FailUsesCharm ? st.PFail : 0;
                    }

            Solve(m, n, n + ColCount);

            int a0 = ClampAura(cfg, aura), g0 = FloorGapState(cfg, stage, floorPeak);
            int start = (stage * auraN + a0) * gapN + g0;
            return new ExpectedValue
            {
                Attempts = m[start, n + ColAttempts],
                Gold = m[start, n + ColGold],
                StonesLow = m[start, n + ColLow],
                StonesMid = m[start, n + ColMid],
                StonesHigh = m[start, n + ColHigh],
                Charms = m[start, n + ColCharm],
            };
        }

        static void Subtract(double[,] m, int row, double p, int s, int a, int g, int t, int auraN, int gapN)
        {
            if (p == 0 || s >= t) return;   // 목표에 닿은 상태는 흡수(E = 0)
            m[row, (s * auraN + a) * gapN + g] -= p;
        }

        /// <summary>부분 피벗 가우스·요르단 소거. 끝나면 오른쪽 열들이 해다.</summary>
        static void Solve(double[,] m, int n, int cols)
        {
            for (int c = 0; c < n; c++)
            {
                int pivot = c;
                double best = Math.Abs(m[c, c]);
                for (int r = c + 1; r < n; r++)
                {
                    double v = Math.Abs(m[r, c]);
                    if (v > best) { best = v; pivot = r; }
                }
                if (best == 0) throw new InvalidOperationException("enhance expectation matrix is singular");
                if (pivot != c)
                    for (int k = c; k < cols; k++)
                    {
                        double tmp = m[c, k]; m[c, k] = m[pivot, k]; m[pivot, k] = tmp;
                    }
                double d = m[c, c];
                for (int k = c; k < cols; k++) m[c, k] /= d;
                for (int r = 0; r < n; r++)
                {
                    if (r == c) continue;
                    double factor = m[r, c];
                    if (factor == 0) continue;
                    for (int k = c; k < cols; k++) m[r, k] -= factor * m[c, k];
                }
            }
        }

        /// <summary>기댓값 표: 목표 = 지금 단계 + 1 … upToStage(최고 단계까지). 제물·부적 없음 가정.</summary>
        public static List<ExpectedRow> ExpectedTable(EnhanceConfig cfg, int stage, int aura, int floorPeak, int upToStage)
        {
            var rows = new List<ExpectedRow>();
            int last = Math.Min(upToStage, cfg.MaxStage);
            for (int target = stage + 1; target <= last; target++)
            {
                ExpectedValue v = Expected(cfg, stage, aura, floorPeak, target, new ExpectedOptions());
                rows.Add(new ExpectedRow
                {
                    TargetStage = target,
                    ExpectedAttempts = v.Attempts,
                    ExpectedGold = v.Gold,
                    ExpectedStonesLow = v.StonesLow,
                    ExpectedStonesMid = v.StonesMid,
                    ExpectedStonesHigh = v.StonesHigh,
                });
            }
            return rows;
        }

        /// <summary>
        /// "보유 재화로 평균 N번"(07 요약서 6절 [임시 U-12]): 지금 상태의 분포를 한 시도씩 앞으로 굴리며 시도 k의
        /// 기대 골드·강화석을 쌓는다. 쌓인 기대 골드가 보유 골드를 넘거나 어느 등급의 쌓인 기대 강화석이 보유량을 넘기
        /// 직전까지의 k. 목표에 닿은 몫은 더 도전하지 않는다. 최대 maxAttempts.
        /// </summary>
        public static int AttemptsWithOwned(EnhanceConfig cfg, int stage, int aura, int floorPeak, int target,
            long gold, int stonesLow, int stonesMid, int stonesHigh, int maxAttempts)
        {
            int t = Math.Min(target, cfg.MaxStage);
            if (stage >= t || stage < 0) return 0;
            int auraN = cfg.AuraMax + 1, gapN = cfg.FloorGap + 1;
            int n = t * auraN * gapN;
            var dist = new double[n];
            var next = new double[n];
            dist[(stage * auraN + ClampAura(cfg, aura)) * gapN + FloorGapState(cfg, stage, floorPeak)] = 1;

            var steps = new Step[n];
            var opt = new ExpectedOptions();
            for (int s = 0; s < t; s++)
                for (int a = 0; a < auraN; a++)
                    for (int g = 0; g < gapN; g++)
                        steps[(s * auraN + a) * gapN + g] = StepOf(cfg, s, a, g, opt);

            double sumGold = 0;
            var sumStone = new double[ColCount];
            var owned = new double[ColCount];
            owned[(int)StoneTier.Low] = stonesLow; owned[(int)StoneTier.Mid] = stonesMid; owned[(int)StoneTier.High] = stonesHigh;
            var stepStone = new double[ColCount];

            for (int k = 1; k <= maxAttempts; k++)
            {
                double stepGold = 0, alive = 0;
                Array.Clear(stepStone, 0, stepStone.Length);
                for (int i = 0; i < n; i++)
                {
                    double w = dist[i];
                    if (w == 0) continue;
                    int s = i / (auraN * gapN);
                    alive += w;
                    stepGold += w * Cost(cfg, s);
                    stepStone[(int)StoneTierFor(cfg, s)] += w * cfg.StonesPerAttempt;
                }
                if (alive <= 0) return k - 1;
                if (sumGold + stepGold > gold) return k - 1;
                for (int tier = (int)StoneTier.Low; tier <= (int)StoneTier.High; tier++)
                    if (sumStone[tier] + stepStone[tier] > owned[tier]) return k - 1;
                sumGold += stepGold;
                for (int tier = (int)StoneTier.Low; tier <= (int)StoneTier.High; tier++) sumStone[tier] += stepStone[tier];

                Array.Clear(next, 0, n);
                for (int i = 0; i < n; i++)
                {
                    double w = dist[i];
                    if (w == 0) continue;
                    int s = i / (auraN * gapN);
                    int rest = i % (auraN * gapN);
                    int g = rest % gapN;
                    Step st = steps[i];
                    Move(next, w * st.PGreat, st.GreatStage, 0, st.GreatGap, t, auraN, gapN);
                    Move(next, w * st.PSuccess, st.SuccessStage, 0, st.SuccessGap, t, auraN, gapN);
                    Move(next, w * st.PMutation, s, st.AuraUp, g, t, auraN, gapN);
                    Move(next, w * st.PFail, st.FailStage, st.FailAura, st.FailGap, t, auraN, gapN);
                }
                var swap = dist; dist = next; next = swap;
            }
            return maxAttempts;
        }

        static void Move(double[] next, double p, int s, int a, int g, int t, int auraN, int gapN)
        {
            if (p == 0 || s >= t) return;
            next[(s * auraN + a) * gapN + g] += p;
        }
    }
}
