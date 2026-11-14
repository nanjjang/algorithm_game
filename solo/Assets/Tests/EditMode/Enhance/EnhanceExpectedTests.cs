using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>
    /// 07 요약서 12.6절(기댓값 마르코프 정확값). 명세 값은 소수 둘째(일부 셋째) 자리까지 적혀 있어 그 자릿수의 반올림 폭으로 견준다.
    /// 08 요약서 10.9절 TV-135~138도 같은 값이다.
    /// </summary>
    public class EnhanceExpectedTests
    {
        const double Tol2 = 0.005 + 1e-9, Tol3 = 0.0005 + 1e-9, TolGold = 0.5 + 1e-6;

        static EnhanceConfig Cfg => TestContent.Db.Enhance;

        static ExpectedValue E(int stage, int aura, int floorPeak, int target, ExpectedOptions opt = default) =>
            EnhanceRules.Expected(Cfg, stage, aura, floorPeak, target, opt);

        static void AssertStones(ExpectedValue v, double low, double mid, double high)
        {
            Assert.AreEqual(low, v.StonesLow, Tol2, "low stones");
            Assert.AreEqual(mid, v.StonesMid, Tol2, "mid stones");
            Assert.AreEqual(high, v.StonesHigh, Tol2, "high stones");
        }

        [Test]
        public void S07_TV_MK_01__ZeroToPlus5()
        {
            ExpectedValue v = E(0, 0, 0, 5);
            Assert.AreEqual(6.71, v.Attempts, Tol2);
            Assert.AreEqual(8517, v.Gold, TolGold);
            AssertStones(v, 6.71, 0, 0);
        }

        [Test]
        public void S07_TV_MK_02__ZeroToPlus7()
        {
            ExpectedValue v = E(0, 0, 0, 7);
            Assert.AreEqual(14.78, v.Attempts, Tol2);
            Assert.AreEqual(37461, v.Gold, TolGold);
            AssertStones(v, 9.04, 5.74, 0);
        }

        [Test]
        public void S07_TV_MK_03__ZeroToPlus10()
        {
            ExpectedValue v = E(0, 0, 0, 10);
            Assert.AreEqual(49.01, v.Attempts, Tol2);
            Assert.AreEqual(273628, v.Gold, TolGold);
            AssertStones(v, 9.04, 36.09, 3.88);
        }

        [Test]
        public void S07_TV_MK_04__ZeroToPlus13()
        {
            ExpectedValue v = E(0, 0, 0, 13);
            Assert.AreEqual(174.92, v.Attempts, Tol2);
            Assert.AreEqual(1776723, v.Gold, TolGold);
            AssertStones(v, 9.04, 48.11, 117.77);
        }

        [Test]
        public void S07_TV_MK_05__StateCountIs168()
        {
            Assert.AreEqual(168, EnhanceRules.ExpectedStateCount(Cfg));   // 14 × 4 × 3
        }

        [Test]
        public void S07_TV_MK_06__Plus7Aura2Peak8ToPlus10()
        {
            ExpectedValue v = E(7, 2, 8, 10);
            Assert.AreEqual(30.40, v.Attempts, Tol2);
            Assert.AreEqual(218423, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_07__Plus7ToPlus8()
        {
            ExpectedValue v = E(7, 0, 7, 8);
            Assert.AreEqual(7.70, v.Attempts, Tol2);
            Assert.AreEqual(39617, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_08__Plus9AtFloorToPlus10()
        {
            ExpectedValue v = E(9, 0, 11, 10);   // g = 2
            Assert.AreEqual(3.18, v.Attempts, Tol2);
            Assert.AreEqual(31761, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_09__Plus12ToPlus13ByAura()
        {
            ExpectedValue a0 = E(12, 0, 12, 13);
            Assert.AreEqual(63.15, a0.Attempts, Tol2);
            Assert.AreEqual(849523, a0.Gold, TolGold);
            ExpectedValue a3 = E(12, 3, 12, 13);
            Assert.AreEqual(29.49, a3.Attempts, Tol2);
            Assert.AreEqual(399489, a3.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_10__WithoutFloorRule()
        {
            // 바닥 규칙 없음(실패마다 −1, +3 이하 하락 없음은 유지)
            ExpectedValue v = E(0, 0, 0, 13, new ExpectedOptions { NoFloor = true });
            Assert.AreEqual(1265.15, v.Attempts, Tol2);
            Assert.AreEqual(8769262, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_11__AttemptsByTarget()
        {
            double[] expected = { 1.000, 2.105, 3.190, 4.508, 6.712, 10.053, 14.785, 21.629, 32.092, 49.008, 75.962, 116.776, 174.921 };
            for (int t = 1; t <= 13; t++) Assert.AreEqual(expected[t - 1], E(0, 0, 0, t).Attempts, Tol3, "target +" + t);
        }

        [Test]
        public void S07_TV_MK_12__GoldByTarget()
        {
            double[] expected = { 100, 542, 1518, 3627, 8517, 18541, 37461, 72668, 140091, 273628, 530301, 994531, 1776723 };
            for (int t = 1; t <= 13; t++) Assert.AreEqual(expected[t - 1], E(0, 0, 0, t).Gold, TolGold, "target +" + t);
        }

        [Test]
        public void S07_TV_MK_13__Plus3ToPlus4()
        {
            ExpectedValue v = E(3, 0, 3, 4);
            Assert.AreEqual(1.511, v.Attempts, Tol3);
            Assert.AreEqual(2417, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_14__Plus4ToPlus5()
        {
            ExpectedValue v = E(4, 0, 4, 5);
            Assert.AreEqual(2.536, v.Attempts, Tol3);
            Assert.AreEqual(5626, v.Gold, TolGold);
        }

        [Test]
        public void S07_TV_MK_20__SacrificePlus15()
        {
            var opt = new ExpectedOptions { SacrificePct = 15 };
            int[] targets = { 5, 7, 10, 13 };
            double[] attempts = { 5.69, 10.45, 24.85, 56.10 };
            double[] gold = { 6800, 24989, 129247, 516618 };
            for (int i = 0; i < targets.Length; i++)
            {
                ExpectedValue v = E(0, 0, 0, targets[i], opt);
                Assert.AreEqual(attempts[i], v.Attempts, Tol2, "target +" + targets[i]);
                Assert.AreEqual(gold[i], v.Gold, TolGold, "target +" + targets[i]);
            }
            AssertStones(E(0, 0, 0, 13, opt), 6.57, 18.26, 31.28);
        }

        [Test]
        public void S07_TV_MK_21__SacrificePlus30()
        {
            var opt = new ExpectedOptions { SacrificePct = 30 };
            int[] targets = { 5, 7, 10, 13 };
            double[] attempts = { 5.12, 8.33, 16.34, 29.93 };
            double[] gold = { 5746, 18628, 79280, 255560 };
            for (int i = 0; i < targets.Length; i++)
            {
                ExpectedValue v = E(0, 0, 0, targets[i], opt);
                Assert.AreEqual(attempts[i], v.Attempts, Tol2, "target +" + targets[i]);
                Assert.AreEqual(gold[i], v.Gold, TolGold, "target +" + targets[i]);
            }
            AssertStones(E(0, 0, 0, 13, opt), 5.45, 9.76, 14.73);
        }

        [Test]
        public void S07_TV_MK_22__SacrificePlus15WithCharmFromStage10()
        {
            // 현재 단계 ≥ 10에서 부적(하락만 막음, 기운 안 쌓임)
            var opt = new ExpectedOptions { SacrificePct = 15, UseCharm = true, CharmFromStage = 10 };
            ExpectedValue v = E(0, 0, 0, 13, opt);
            Assert.AreEqual(34.70, v.Attempts, Tol2);
            Assert.AreEqual(272826, v.Gold, TolGold);
            Assert.AreEqual(5.42, v.Charms, Tol2);
            AssertStones(v, 6.57, 15.88, 12.26);
        }

        // ------------------------------------------------------------ 몬테카를로 회귀(규칙 함수를 그대로 돌린다)

        /// <summary>한 자루를 +0에서 목표까지 올린다(제물 보너스 고정, 변이는 받는다). 판정은 강화 서비스와 같은 규칙 함수.</summary>
        static void Climb(Pcg32 rng, int sacrificePct, int target, out int attempts, out long gold)
        {
            EnhanceConfig cfg = Cfg;
            int stage = 0, peak = 0, aura = 0;
            attempts = 0; gold = 0;
            while (stage < target)
            {
                attempts++;
                gold += EnhanceRules.Cost(cfg, stage);
                int pct = EnhanceRules.SuccessPct(cfg, stage, aura, sacrificePct);
                JudgeBand band = EnhanceRules.Judge(rng.Range(0, EnhanceRules.BpFull), EnhanceRules.SuccessBp(pct),
                    EnhanceRules.GreatBp(cfg, stage, pct), EnhanceRules.MutationEndBp(cfg, pct));
                if (band == JudgeBand.GreatSuccess) { stage = EnhanceRules.StageAfterGreat(cfg, stage); aura = 0; }
                else if (band == JudgeBand.Success) { stage = EnhanceRules.StageAfterSuccess(cfg, stage); aura = 0; }
                else if (band == JudgeBand.Mutation) aura = Math.Min(cfg.AuraMax, aura + 1);
                else
                {
                    FallbackResult fb = EnhanceRules.ResolveFail(cfg, stage, peak, aura, false);
                    stage = fb.StageAfter; aura = fb.AuraAfter;
                }
                if (stage > peak) peak = stage;
            }
        }

        [Test, Category("Slow")]
        public void S07_TV_MC_01__GoldMedianAndTopTenPercentWithSacrifice15()
        {
            const int runs = 20000;
            var rng = new Pcg32(20261114);
            var golds = new List<long>(runs);
            for (int i = 0; i < runs; i++)
            {
                Climb(rng, 15, 13, out _, out long gold);
                golds.Add(gold);
            }
            golds.Sort();
            Assert.AreEqual(451_000, golds[runs / 2], 451_000 * 0.03, "gold median");
            Assert.AreEqual(925_000, golds[runs * 9 / 10], 925_000 * 0.03, "top 10% boundary");
        }

        [Test, Category("Slow")]
        public void S07_TV_MC_02__SimulatedMeanAttemptsMatchMarkov()
        {
            const int runs = 20000;
            var rng = new Pcg32(4);
            double sum = 0;
            for (int i = 0; i < runs; i++)
            {
                Climb(rng, 15, 13, out int attempts, out _);
                sum += attempts;
            }
            Assert.AreEqual(56.10, sum / runs, 56.10 * 0.02);   // TV-MK-20과 ±2%
        }

        [Test, Category("Slow")]
        public void S07_6__ServiceWalkMatchesExpectedTable()
        {
            // 강화 서비스를 그대로 돌린 평균이 기댓값 표(제물·부적 없음, 변이는 받는다)와 맞는다: +0 → +7 평균 14.78회 / 37,461골드.
            const int runs = 4000;
            var kit = new EnhanceKit(new Pcg32EnhanceRandom(new Pcg32(31)));
            WandData w = kit.Wand();
            double attempts = 0, gold = 0;
            for (int i = 0; i < runs; i++)
            {
                kit.Set(0, 0, 0);
                w.Mutations.Clear();
                w.DropCount = 0;
                kit.SetSlots(w, 0);
                while (w.Stage < 7)
                {
                    kit.Gold = 1_000_000;
                    kit.Profile.Item(Ids.Item.StoneLow).Qty = 1;
                    kit.Profile.Item(Ids.Item.StoneMid).Qty = 1;
                    if (w.PendingOffer != null)
                    {
                        Assert.AreEqual(ErrorCode.None, kit.Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId, true).Error);
                        continue;
                    }
                    Result<EnhanceResult> r = kit.Service.Enhance(w.Id, null, false);
                    Assert.AreEqual(ErrorCode.None, r.Error);
                    attempts++;
                    gold += r.Value.Breakdown.CostGold;
                }
            }
            Assert.AreEqual(14.78, attempts / runs, 14.78 * 0.03);
            Assert.AreEqual(37461, gold / runs, 37461 * 0.03);
        }

        // ------------------------------------------------------------ 기댓값 표(화면), 보유 재화로 평균 N번

        [Test]
        public void S07_6__ExpectedTableRowsFromCurrentState()
        {
            var kit = new EnhanceKit();
            WandData w = kit.Set(7, 8, 2);
            List<ExpectedRow> rows = kit.Service.ExpectedTable(w.Id, 13);
            Assert.AreEqual(6, rows.Count);
            Assert.AreEqual(8, rows[0].TargetStage);
            Assert.AreEqual(13, rows[5].TargetStage);
            ExpectedRow plus10 = rows[2];
            Assert.AreEqual(10, plus10.TargetStage);
            Assert.AreEqual(30.40, plus10.ExpectedAttempts, Tol2);    // TV-MK-06
            Assert.AreEqual(218423, plus10.ExpectedGold, TolGold);
            for (int i = 1; i < rows.Count; i++) Assert.Greater(rows[i].ExpectedAttempts, rows[i - 1].ExpectedAttempts);
            Assert.AreEqual(0, kit.Service.ExpectedTable(999, 13).Count);
            Assert.AreEqual(0, kit.Service.ExpectedTable(kit.Set(13, 13, 0).Id, 13).Count);
            Assert.AreEqual(3, kit.Service.ExpectedTable(kit.Set(7, 8, 2).Id, 10).Count);
        }

        [Test]
        public void S07_6__ExpectedStonesAddUpToAttempts()
        {
            ExpectedValue v = E(2, 1, 4, 13);
            Assert.AreEqual(v.Attempts, v.StonesLow + v.StonesMid + v.StonesHigh, 1e-6);
            Assert.AreEqual(0, E(13, 0, 13, 13).Attempts);
            Assert.AreEqual(0, E(9, 0, 9, 5).Attempts, "already past the target");
        }

        [Test]
        public void S07_6__DefaultTargetIsNextMilestone()
        {
            int[] next = { 4, 4, 4, 4, 7, 7, 7, 10, 10, 10, 13, 13, 13, 13 };
            for (int s = 0; s <= 13; s++) Assert.AreEqual(next[s], EnhanceRules.NextMilestone(Cfg, s), "stage " + s);
        }

        [Test]
        public void S07_6__AttemptsWithOwnedGoods()
        {
            // 한 시도씩 앞으로 굴리며 기대 비용을 쌓는다. +0에서 첫 세 시도는 반드시 100 / 400(또는 다시 400…)…
            // 골드 100이면 정확히 1번, 99면 0번. 강화석이 0개면 0번.
            Assert.AreEqual(1, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 100, 10, 10, 10, 1000));
            Assert.AreEqual(0, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 99, 10, 10, 10, 1000));
            Assert.AreEqual(0, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 1_000_000, 0, 10, 10, 1000));
            // 둘째 시도의 기대 비용은 400(첫 시도는 100% 성공) → 골드 500이면 2번, 499면 1번.
            Assert.AreEqual(2, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 500, 10, 10, 10, 1000));
            Assert.AreEqual(1, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 499, 10, 10, 10, 1000));
            // 하급 강화석이 1개면 둘째 시도(하급)에서 모자란다.
            Assert.AreEqual(1, EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 13, 1_000_000, 1, 10, 10, 1000));
            // 재화가 넘치면 상한(1,000)에 닿기 전에 목표에 닿은 몫이 빠져 끝나거나 상한에서 멈춘다.
            int many = EnhanceRules.AttemptsWithOwned(Cfg, 0, 0, 0, 1, long.MaxValue, 1000, 1000, 1000, 1000);
            Assert.AreEqual(1, many, "target +1 is reached with the first attempt");
            Assert.LessOrEqual(EnhanceRules.AttemptsWithOwned(Cfg, 12, 0, 12, 13, long.MaxValue, 100000, 100000, 100000, 1000), 1000);
            // 많이 가질수록 줄지 않는다.
            int prev = 0;
            foreach (long gold in new long[] { 1000, 5000, 20000, 100000, 500000 })
            {
                int n = EnhanceRules.AttemptsWithOwned(Cfg, 5, 0, 5, 10, gold, 1000, 1000, 1000, 1000);
                Assert.GreaterOrEqual(n, prev);
                prev = n;
            }

            var kit = new EnhanceKit();
            kit.Gold = 500;
            kit.Give(Ids.Item.StoneLow, 10);
            Assert.AreEqual(2, kit.Service.AttemptsWithOwned(kit.Wand().Id, 13));
            Assert.AreEqual(1000, kit.Service.Tuning.OwnedForecastMaxAttempts);
        }
    }
}
