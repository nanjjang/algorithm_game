using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>
    /// 07 요약서 12.8절 경제 시뮬레이션 회귀(TV-E-*). docs/sim/online_econ3*.py와 같은 모형(EconModel)을 돌린다.
    /// 허용 오차는 명세 그대로: 평균 ±10%(1,500명), 슬롯 낚시 ±3%(20만 자루), 시간당 수입 ±10%.
    /// 명세 값은 반올림해 적혀 있어("65", "0.07") 마지막 자리의 반올림 폭을 허용 오차에 더한다. 무거워서 Slow 분류다.
    /// </summary>
    [Category("Slow")]
    public class EnhanceEconomyTests
    {
        const double Normal = 1.74, Beginner = 1.42, Skilled = 2.62;   // 모형의 실력 계수 k
        const ulong Seed = 11;

        static EconModel Model(char timeModel) => new EconModel(TestContent.Db, timeModel);

        /// <summary>expected ± (rel × expected + 적힌 마지막 자리의 절반).</summary>
        static void Near(double expected, double actual, double rel, double unit, string what)
        {
            double tol = Math.Abs(expected) * rel + unit / 2;
            Assert.AreEqual(expected, actual, tol, what);
        }

        static List<CareerResult> Careers(EconModel m, double k, double share = 1.0, bool sac = true, bool charm = false, bool freeStones = false)
        {
            var rng = new EconRandom(Seed);
            var list = new List<CareerResult>(EconModel.Careers);
            for (int i = 0; i < EconModel.Careers; i++) list.Add(m.Career(rng, k, share, sac, charm, freeStones));
            return list;
        }

        static Stat3 Hours(List<CareerResult> careers, int stage)
        {
            var v = new List<double>(careers.Count);
            foreach (CareerResult c in careers) v.Add(c.HitOr(stage, EconModel.MaxHours));
            return Stat3.Of(v);
        }

        /// <summary>평균은 명세의 허용 ±10%. 중앙값·운 나쁜 10%도 같은 폭으로 본다.</summary>
        static void AssertHours(Stat3 s, double mean, double median, double p90, string what)
        {
            double unit = mean < 10 ? 0.1 : 1;
            Near(mean, s.Mean, 0.10, unit, what + " mean");
            Near(median, s.Median, 0.10, unit, what + " median");
            Near(p90, s.P90, 0.10, p90 < 10 ? 0.1 : 1, what + " p90");
        }

        static double[] SlotShare(List<CareerResult> careers)
        {
            var share = new double[4];
            foreach (CareerResult c in careers) share[c.Slots] += 100.0 / careers.Count;
            return share;
        }

        static double MeanAttempts(List<CareerResult> careers)
        {
            double sum = 0;
            foreach (CareerResult c in careers) sum += c.Attempts;
            return sum / careers.Count;
        }

        // ------------------------------------------------------------ +13에 처음 닿기까지(기본 판 길이)

        [Test]
        public void S07_TV_E_01__NormalSacrificeAllGold()
        {
            List<CareerResult> c = Careers(Model('A'), Normal);
            AssertHours(Hours(c, 7), 4.4, 3.9, 7.1, "+7");
            AssertHours(Hours(c, 10), 19, 16, 35, "+10");
            AssertHours(Hours(c, 13), 65, 59, 112, "+13");
        }

        [Test]
        public void S07_TV_E_02__NormalSacrifice70PercentGold()
        {
            List<CareerResult> c = Careers(Model('A'), Normal, share: 0.7);
            AssertHours(Hours(c, 7), 6.1, 5.3, 10, "+7");
            AssertHours(Hours(c, 10), 27, 23, 49, "+10");
            AssertHours(Hours(c, 13), 90, 80, 157, "+13");
        }

        [Test]
        public void S07_TV_E_03__NormalSacrificeCharmFromPlus10AllGold()
        {
            List<CareerResult> c = Careers(Model('A'), Normal, charm: true);
            AssertHours(Hours(c, 7), 4.6, 4.0, 7.6, "+7");
            AssertHours(Hours(c, 10), 20, 17, 36, "+10");
            AssertHours(Hours(c, 13), 37, 35, 56, "+13");
        }

        [Test]
        public void S07_TV_E_04__NormalSacrificeCharmFromPlus10SeventyPercentGold()
        {
            List<CareerResult> c = Careers(Model('A'), Normal, share: 0.7, charm: true);
            AssertHours(Hours(c, 7), 6.3, 5.5, 10, "+7");
            AssertHours(Hours(c, 10), 27, 23, 49, "+10");
            AssertHours(Hours(c, 13), 51, 48, 79, "+13");
        }

        [Test]
        public void S07_TV_E_05__NormalNoSacrifice()
        {
            List<CareerResult> c = Careers(Model('A'), Normal, sac: false);
            AssertHours(Hours(c, 7), 6.5, 5.3, 12, "+7");
            AssertHours(Hours(c, 10), 41, 35, 75, "+10");
            AssertHours(Hours(c, 13), 220, 192, 399, "+13");
        }

        [Test]
        public void S07_TV_E_06__Beginner()
        {
            List<CareerResult> c = Careers(Model('A'), Beginner);
            AssertHours(Hours(c, 7), 5.3, 4.6, 8.8, "+7");
            AssertHours(Hours(c, 10), 21, 18, 37, "+10");
            AssertHours(Hours(c, 13), 73, 64, 130, "+13");
        }

        [Test]
        public void S07_TV_E_07__Skilled()
        {
            List<CareerResult> c = Careers(Model('A'), Skilled);
            AssertHours(Hours(c, 7), 3.7, 3.3, 6.2, "+7");
            AssertHours(Hours(c, 10), 16, 14, 28, "+10");
            AssertHours(Hours(c, 13), 58, 51, 102, "+13");
        }

        [Test]
        public void S07_TV_E_08__StonesUnlimitedChangesNothing()
        {
            // 막히는 자원은 골드다: 강화석을 무제한으로 줘도 +13까지의 시간이 같다.
            AssertHours(Hours(Careers(Model('A'), Normal, freeStones: true), 13), 65, 58, 111, "+13");
        }

        // ------------------------------------------------------------ 빠른 쪽 판 길이(+13)

        [Test]
        public void S07_TV_E_09__FastNormalAllGold() => AssertHours(Hours(Careers(Model('B'), Normal), 13), 44, 40, 75, "+13");

        [Test]
        public void S07_TV_E_10__FastNormal70PercentGold() => AssertHours(Hours(Careers(Model('B'), Normal, share: 0.7), 13), 61, 54, 105, "+13");

        [Test]
        public void S07_TV_E_11__FastCharmFromPlus10() => AssertHours(Hours(Careers(Model('B'), Normal, charm: true), 13), 25, 24, 38, "+13");

        [Test]
        public void S07_TV_E_12__FastNoSacrifice() => AssertHours(Hours(Careers(Model('B'), Normal, sac: false), 13), 147, 128, 265, "+13");

        [Test]
        public void S07_TV_E_13__FastBeginner() => AssertHours(Hours(Careers(Model('B'), Beginner), 13), 51, 45, 89, "+13");

        [Test]
        public void S07_TV_E_14__FastSkilled() => AssertHours(Hours(Careers(Model('B'), Skilled), 13), 36, 32, 64, "+13");

        // ------------------------------------------------------------ +13 때 확장 슬롯 분포

        [Test]
        public void S07_TV_E_15__SlotDistributionAtPlus13()
        {
            // 3칸 79%, 2칸 15%, 1칸 5%, 0칸 1%. 시도 수 57. (분포의 허용 폭은 명세에 없어 ±5%p로 본다.)
            List<CareerResult> c = Careers(Model('A'), Normal);
            double[] share = SlotShare(c);
            Assert.AreEqual(79, share[3], 5, "3 slots");
            Assert.AreEqual(15, share[2], 5, "2 slots");
            Assert.AreEqual(5, share[1], 5, "1 slot");
            Assert.AreEqual(1, share[0], 5, "0 slots");
            Near(57, MeanAttempts(c), 0.10, 1, "attempts");
        }

        [Test]
        public void S07_TV_E_16__SlotDistributionWithCharm()
        {
            // +10부터 부적: 48 / 30 / 18 / 4%. 시도 수 57 → 37.
            List<CareerResult> c = Careers(Model('A'), Normal, charm: true);
            double[] share = SlotShare(c);
            Assert.AreEqual(48, share[3], 5, "3 slots");
            Assert.AreEqual(30, share[2], 5, "2 slots");
            Assert.AreEqual(18, share[1], 5, "1 slot");
            Assert.AreEqual(4, share[0], 5, "0 slots");
            Near(37, MeanAttempts(c), 0.10, 1, "attempts");
        }

        // ------------------------------------------------------------ 여러 자루

        static Stat3 Multi(char tm, MultiPolicy policy, int n, int target = 7)
        {
            EconModel m = Model(tm);
            var rng = new EconRandom(Seed);
            var v = new List<double>(EconModel.Careers);
            for (int i = 0; i < EconModel.Careers; i++) v.Add(m.CareerMulti(rng, Normal, policy, n, target).Hours);
            return Stat3.Of(v);
        }

        [Test]
        public void S07_TV_E_20__OneWand() => AssertHours(Multi('A', MultiPolicy.Single, 1), 65, 59, 112, "single");

        [Test]
        public void S07_TV_E_21__SeveralWandsLeaderOnly()
        {
            // 2·3·5·10자루 사 두고 가장 높은 한 자루만: 64~66 / 56~59 / 111~115
            foreach (int n in new[] { 2, 3, 5, 10 })
            {
                Stat3 s = Multi('A', MultiPolicy.Leader, n);
                Assert.That(s.Mean, Is.InRange(64 * 0.9, 66 * 1.1), n + " wands mean");
                Assert.That(s.Median, Is.InRange(56 * 0.9, 59 * 1.1), n + " wands median");
                Assert.That(s.P90, Is.InRange(111 * 0.9, 115 * 1.1), n + " wands p90");
            }
        }

        [Test]
        public void S07_TV_E_22__WandsKeptLevel()
        {
            AssertHours(Multi('A', MultiPolicy.Even, 2), 99, 91, 157, "2 wands");
            AssertHours(Multi('A', MultiPolicy.Even, 3), 134, 127, 201, "3 wands");
            AssertHours(Multi('A', MultiPolicy.Even, 5), 202, 196, 280, "5 wands");
        }

        [Test]
        public void S07_TV_E_23__ThreeWandsLiftedThenLeader()
        {
            AssertHours(Multi('A', MultiPolicy.Lift, 3, 5), 67, 60, 113, "to +5");
            AssertHours(Multi('A', MultiPolicy.Lift, 3, 7), 74, 67, 122, "to +7");
        }

        [Test]
        public void S07_TV_E_24__FiveAndTenWandsLiftedToPlus7()
        {
            AssertHours(Multi('A', MultiPolicy.Lift, 5, 7), 78, 70, 130, "5 wands");
            AssertHours(Multi('A', MultiPolicy.Lift, 10, 7), 99, 92, 151, "10 wands");
        }

        [Test]
        public void S07_TV_E_25__ThreeWandsLiftedToPlus10() => AssertHours(Multi('A', MultiPolicy.Lift, 3, 10), 92, 86, 141, "to +10");

        [Test]
        public void S07_TV_E_26__SwitchWandAfterEveryFailure()
        {
            AssertHours(Multi('A', MultiPolicy.Switch, 2), 84, 75, 150, "2 wands");
            AssertHours(Multi('A', MultiPolicy.Switch, 3), 96, 87, 164, "3 wands");
        }

        [Test]
        public void S07_TV_E_27__FastPace()
        {
            AssertHours(Multi('B', MultiPolicy.Single, 1), 44, 40, 75, "single");
            AssertHours(Multi('B', MultiPolicy.Even, 3), 88, 84, 131, "3 kept level");
            AssertHours(Multi('B', MultiPolicy.Lift, 3, 7), 50, 45, 81, "3 to +7 then leader");
        }

        [Test]
        public void S07_TV_E_28__NoPolicyBeatsOneWand()
        {
            // 불변식: 어느 방법도 한 자루보다 평균이 작지 않다(허용 1시간).
            double single = Multi('A', MultiPolicy.Single, 1).Mean;
            var all = new List<KeyValuePair<string, double>>();
            foreach (int n in new[] { 2, 3, 5, 10 }) all.Add(new KeyValuePair<string, double>("leader " + n, Multi('A', MultiPolicy.Leader, n).Mean));
            foreach (int n in new[] { 2, 3, 5 }) all.Add(new KeyValuePair<string, double>("even " + n, Multi('A', MultiPolicy.Even, n).Mean));
            all.Add(new KeyValuePair<string, double>("lift 3/5", Multi('A', MultiPolicy.Lift, 3, 5).Mean));
            all.Add(new KeyValuePair<string, double>("lift 3/7", Multi('A', MultiPolicy.Lift, 3, 7).Mean));
            all.Add(new KeyValuePair<string, double>("lift 5/7", Multi('A', MultiPolicy.Lift, 5, 7).Mean));
            all.Add(new KeyValuePair<string, double>("lift 10/7", Multi('A', MultiPolicy.Lift, 10, 7).Mean));
            all.Add(new KeyValuePair<string, double>("lift 3/10", Multi('A', MultiPolicy.Lift, 3, 10).Mean));
            foreach (int n in new[] { 2, 3 }) all.Add(new KeyValuePair<string, double>("switch " + n, Multi('A', MultiPolicy.Switch, n).Mean));
            foreach (KeyValuePair<string, double> kv in all)
                Assert.GreaterOrEqual(kv.Value, single - 1, kv.Key + " vs single " + single.ToString("F1"));
        }

        // ------------------------------------------------------------ 슬롯 낚시(20만 자루, ±3%)

        const int FishWands = 200000;
        const ulong FishSeed = 5;

        [Test]
        public void S07_TV_E_30__FishToPlus4()
        {
            EconModel.FishResult f = Model('A').Fish(4, FishWands, FishSeed);
            Near(3300, f.Gold, 0.03, 100, "gold");
            Near(4.3, f.StonesPerWand[0], 0.03, 0.1, "low stones");
            Near(38.5, f.AtLeast[1] * 100, 0.03, 0.1, ">= 1 slot %");
            Near(2.3, f.AtLeast[2] * 100, 0.03, 0.1, ">= 2 slots %");
            Assert.AreEqual(0, f.AtLeast[3], "3 slots are impossible by +4");
            // 그런 한 자루 값(+0 지팡이 값 포함): 1칸 9,900골드, 하급 11, 2.6자루
            double price = TestContent.Db.Economy.ShopWandPlus0Gold;
            Near(9900, (f.Gold + price) / f.AtLeast[1], 0.03, 100, "gold per 1-slot wand");
            Near(11, f.StonesPerWand[0] / f.AtLeast[1], 0.03, 1, "low stones per 1-slot wand");
            Near(2.6, 1 / f.AtLeast[1], 0.03, 0.1, "wands per 1-slot wand");
        }

        [Test]
        public void S07_TV_E_31__FishToPlus5()
        {
            EconModel.FishResult f = Model('A').Fish(5, FishWands, FishSeed);
            Near(6900, f.Gold, 0.03, 100, "gold");
            Near(5.9, f.StonesPerWand[0], 0.03, 0.1, "low stones");
            Near(50.5, f.AtLeast[1] * 100, 0.03, 0.1, ">= 1 slot %");
            Near(7.3, f.AtLeast[2] * 100, 0.03, 0.1, ">= 2 slots %");
            Assert.AreEqual(0, f.AtLeast[3], "3 slots are impossible by +5");
            double price = TestContent.Db.Economy.ShopWandPlus0Gold;
            Near(14700, (f.Gold + price) / f.AtLeast[1], 0.03, 100, "gold per 1-slot wand");
            Near(12, f.StonesPerWand[0] / f.AtLeast[1], 0.03, 1, "low stones per 1-slot wand");
            Near(101700, (f.Gold + price) / f.AtLeast[2], 0.03, 100, "gold per 2-slot wand");
            Near(80, f.StonesPerWand[0] / f.AtLeast[2], 0.03, 1, "low stones per 2-slot wand");
            Near(13.7, 1 / f.AtLeast[2], 0.03, 0.1, "wands per 2-slot wand");
        }

        [Test]
        public void S07_TV_E_32__FishToPlus7()
        {
            EconModel.FishResult f = Model('A').Fish(7, FishWands, FishSeed);
            Near(25100, f.Gold, 0.03, 100, "gold");
            Near(6.8, f.StonesPerWand[0], 0.03, 0.1, "low stones");
            Near(3.9, f.StonesPerWand[1], 0.03, 0.1, "mid stones");
            Near(71.5, f.AtLeast[1] * 100, 0.03, 0.1, ">= 1 slot %");
            Near(26.0, f.AtLeast[2] * 100, 0.03, 0.1, ">= 2 slots %");
            Near(3.8, f.AtLeast[3] * 100, 0.03, 0.1, "3 slots %");
            double price = TestContent.Db.Economy.ShopWandPlus0Gold;
            Near(98600, (f.Gold + price) / f.AtLeast[2], 0.03, 100, "gold per 2-slot wand");
            Near(26, f.StonesPerWand[0] / f.AtLeast[2], 0.03, 1, "low stones per 2-slot wand");
            Near(3.8, 1 / f.AtLeast[2], 0.03, 0.1, "wands per 2-slot wand");
            Near(680000, (f.Gold + price) / f.AtLeast[3], 0.03, 10000, "gold per 3-slot wand");
            Near(26.5, 1 / f.AtLeast[3], 0.03, 0.1, "wands per 3-slot wand");
        }

        // ------------------------------------------------------------ 시간당 수입(±10%)

        const int IncomeRuns = 40000;
        // 명세의 표를 만든 원본 스크립트의 표본 수(online_econ3.static: 판 모양별 4,000판, 단계별 3,000판).
        const int SpecIncomeRuns = 4000, SpecStageRuns = 3000;

        /// <summary>
        /// 부적처럼 드문 드랍의 시간당 개수. 명세 값은 원본의 작은 표본(수십~백여 건)에서 나온 것이라 값 자체에 표본 오차가 있다
        /// (예: 1→14는 모형의 정확값이 판당 2% ÷ 23.3분 = 0.0515/시간인데 4,000판 표본이 0.07로 찍혔다).
        /// 그래서 명세의 ±10%에 원본 표본의 3σ(= 3 / √건수)를 더해 견준다. 기대값은 명세 값 그대로다.
        /// </summary>
        static void NearRare(double expected, double actual, double unit, double minutes, int specRuns, string what)
        {
            double events = expected * (minutes / 60.0) * specRuns;
            double sigma = events > 0 ? 1 / Math.Sqrt(events) : 0;
            Assert.AreEqual(expected, actual, expected * (0.10 + 3 * sigma) + unit / 2, what);
        }

        static void AssertIncome(int start, int reach, double goldPerRun, double minutes, double fastMinutes,
            double net, double fastNet, double lo, double mid, double up, double charm)
        {
            EconModel.Income a = Model('A').StaticIncome(start, reach, IncomeRuns, 1);
            EconModel.Income b = Model('B').StaticIncome(start, reach, IncomeRuns, 1);
            string what = (start == 0 ? 1 : start) + "->" + reach + " ";
            Near(goldPerRun, a.GoldPerRun, 0.10, 10, what + "gold per run");
            Near(minutes, a.Minutes, 0.10, 1, what + "minutes");
            Near(fastMinutes, b.Minutes, 0.10, 1, what + "minutes (fast)");
            Near(net, a.NetGoldPerHour, 0.10, 100, what + "net gold per hour");
            Near(fastNet, b.NetGoldPerHour, 0.10, 100, what + "net gold per hour (fast)");
            Near(lo, a.LoPerHour, 0.10, 0.1, what + "low stones per hour");
            Near(mid, a.MidPerHour, 0.10, 0.1, what + "mid stones per hour");
            Near(up, a.UpPerHour, 0.10, 0.1, what + "high stones per hour");
            NearRare(charm, a.CharmPerHour, 0.01, minutes, SpecIncomeRuns, what + "charms per hour");
        }

        [Test]
        public void S07_TV_E_40__Wave1To9() => AssertIncome(0, 9, 1010, 15, 12, 3500, 4300, 4.1, 0, 0, 0);

        [Test]
        public void S07_TV_E_41__Wave1To14() => AssertIncome(0, 14, 2250, 23, 18, 4900, 6300, 5.9, 1.9, 0, 0.07);

        [Test]
        public void S07_TV_E_42__Wave10To24() => AssertIncome(10, 24, 3770, 26, 18, 7400, 10500, 4.2, 7.0, 0.8, 0.19);

        [Test]
        public void S07_TV_E_43__Wave20To30() => AssertIncome(20, 30, 3550, 21, 14, 8800, 13000, 0, 8.4, 4.1, 0.44);

        [Test]
        public void S07_TV_E_44__Wave20To40() => AssertIncome(20, 40, 8020, 41, 26, 9900, 15900, 0, 9.1, 4.8, 0.52);

        [Test]
        public void S07_TV_E_50__NormalPlayerByStage()
        {
            // 단계: 도달·시작·분·순골드·하/중/상·부적, 빠른 쪽 순골드
            int[] stage = { 0, 3, 5, 7, 9, 10, 13 };
            int[] reach = { 7, 14, 19, 23, 28, 31, 38 };
            int[] start = { 1, 1, 10, 10, 10, 20, 20 };
            double[] minutes = { 12, 23, 16, 24, 33, 23, 37 };
            double[] net = { 3100, 4900, 6200, 7300, 7700, 8800, 9200 };
            double[] lo = { 5.1, 5.9, 6.6, 4.5, 3.3, 0, 0 };
            double[] mid = { 0, 1.9, 6.0, 7.2, 8.0, 8.1, 8.4 };
            double[] up = { 0, 0, 0, 0.8, 1.1, 4.0, 3.5 };
            double[] charm = { 0, 0.06, 0, 0.18, 0.22, 0.43, 0.46 };
            double[] fastNet = { 3700, 6300, 8500, 10300, 11300, 13300, 14800 };
            EconModel a = Model('A'), b = Model('B');
            for (int i = 0; i < stage.Length; i++)
            {
                a.ReachAndStart(Normal, stage[i], out int r, out int s);
                string what = "+" + stage[i] + " ";
                Assert.AreEqual(reach[i], r, what + "reach");
                Assert.AreEqual(start[i], s == 0 ? 1 : s, what + "start");
                EconModel.Income ia = a.StaticIncome(s, r, IncomeRuns, 3);
                EconModel.Income ib = b.StaticIncome(s, r, IncomeRuns, 3);
                Near(minutes[i], ia.Minutes, 0.10, 1, what + "minutes");
                Near(net[i], ia.NetGoldPerHour, 0.10, 100, what + "net gold per hour");
                Near(lo[i], ia.LoPerHour, 0.10, 0.1, what + "low");
                Near(mid[i], ia.MidPerHour, 0.10, 0.1, what + "mid");
                Near(up[i], ia.UpPerHour, 0.10, 0.1, what + "high");
                NearRare(charm[i], ia.CharmPerHour, 0.01, minutes[i], SpecStageRuns, what + "charm");
                Near(fastNet[i], ib.NetGoldPerHour, 0.10, 100, what + "net gold per hour (fast)");
            }
        }

        // ------------------------------------------------------------ 민감도

        [Test]
        public void S07_TV_E_60__StoneDropSensitivity()
        {
            // 중급 드랍 1/5 → +13 65시간 그대로. 중급 1/10·상급 1/3 → +10이 19 → 27시간, +13은 67시간.
            EconModel fifth = Model('A');
            fifth.MidKeep = 0.2;
            Near(65, Hours(Careers(fifth, Normal), 13).Mean, 0.10, 1, "mid x0.2: +13");

            EconModel scarce = Model('A');
            scarce.MidKeep = 0.1; scarce.UpKeep = 0.33;
            List<CareerResult> c = Careers(scarce, Normal);
            Near(27, Hours(c, 10).Mean, 0.10, 1, "mid x0.1, high x0.33: +10");
            Near(67, Hours(c, 13).Mean, 0.10, 1, "mid x0.1, high x0.33: +13");
        }

        // ------------------------------------------------------------ 모형의 기초(07 요약서 7.2절 식과 같은지)

        [Test]
        public void S07_12_8__WaveGoldFormulaOfTheModel()
        {
            // TV-G-01·02와 같은 식을 모형이 쓴다: 1, 5, 9, 14, 42 → 52, 100, 148, 200, 420 / 10, 20, 30, 50 → 400, 600, 800, 1,200
            int[] w = { 1, 5, 9, 14, 42, 10, 20, 30, 50 };
            double[] g = { 52, 100, 148, 200, 420, 400, 600, 800, 1200 };
            for (int i = 0; i < w.Length; i++) Assert.AreEqual(g[i], EconModel.GoldWave(w[i]), 1e-9, "wave " + w[i]);
        }
    }
}
