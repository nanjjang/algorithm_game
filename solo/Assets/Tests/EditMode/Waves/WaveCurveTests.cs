using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>06 요약서 3.1절 곡선 값, 05 요약서 2.1절·3.2절. 기대값은 요약서의 벡터 그대로.</summary>
    public class WaveCurveTests
    {
        static WavesDef W => TestContent.Db.Waves;

        [TestCase(1, 16)]
        [TestCase(2, 22)]
        [TestCase(9, 64)]
        [TestCase(12, 82)]
        public void S07_TV_060__BudgetLinear(int wave, int budget)
        {
            Assert.AreEqual(budget, WaveCurves.Budget(W, wave));
        }

        // 10 요약서 6절: w ≥ 42는 예산 200 고정
        [TestCase(13, 86)]
        [TestCase(20, 114)]
        [TestCase(30, 154)]
        [TestCase(41, 198)]
        [TestCase(42, 200)]
        [TestCase(100, 200)]
        [TestCase(250, 200)]
        public void S07_TV_061__BudgetLateAndCap(int wave, int budget)
        {
            Assert.AreEqual(budget, WaveCurves.Budget(W, wave));
        }

        [TestCase(1, 4)]
        [TestCase(12, 4)]
        [TestCase(13, 4)]
        [TestCase(19, 4)]
        public void S07_TV_062__BundleCountEarly(int wave, int count)
        {
            Assert.AreEqual(count, WaveCurves.BundleCount(W, wave));
        }

        [TestCase(20, 5)]
        [TestCase(27, 5)]
        [TestCase(28, 6)]
        [TestCase(36, 7)]
        [TestCase(44, 8)]
        [TestCase(100, 8)]
        public void S07_TV_063__BundleCountGrowsToEight(int wave, int count)
        {
            Assert.AreEqual(count, WaveCurves.BundleCount(W, wave));
        }

        // 05 요약서 2.1절: 1~4 → 2, 5~8 → 3, 9~12 → 4, 13~29 → 5, 30+ → 6
        [TestCase(1, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(8, 3)]
        [TestCase(9, 4)]
        [TestCase(12, 4)]
        [TestCase(13, 5)]
        [TestCase(29, 5)]
        [TestCase(30, 6)]
        [TestCase(99, 6)]
        public void S07_TV_064__TelegraphCap(int wave, int cap)
        {
            Assert.AreEqual(cap, WaveCurves.TelegraphCap(W, wave));
            Assert.AreEqual(cap, new WaveDirector(TestContent.Db).TelegraphCap(wave));
        }

        [TestCase(11, 0)]
        [TestCase(12, 1)]
        [TestCase(19, 1)]
        [TestCase(21, 2)]
        [TestCase(29, 2)]
        [TestCase(31, 3)]
        [TestCase(39, 3)]
        [TestCase(41, 4)]
        [TestCase(99, 4)]
        public void S07_TV_065__EliteCount(int wave, int count)
        {
            Assert.AreEqual(count, WaveCurves.EliteCount(W, wave));
        }

        [TestCase(20)]
        [TestCase(30)]
        [TestCase(40)]
        public void S07_TV_066__EliteCountZeroOnBossWave(int wave)
        {
            Assert.AreEqual(0, WaveCurves.EliteCount(W, wave));
            Assert.AreEqual(0, WaveCurves.EliteCount(W, wave, TestContent.Db.Modifier(Ids.Whim.ChiefParade).ExtraElites));
        }

        [Test]
        public void S07_TV_067__ChiefParadeAddsTwoElites()
        {
            int extra = TestContent.Db.Modifier(Ids.Whim.ChiefParade).ExtraElites;
            Assert.AreEqual(2, extra);
            Assert.AreEqual(4, WaveCurves.EliteCount(W, 25, extra));
        }

        [TestCase(20, 0)]
        public void S07_TV_100__NoWhimOnBossWave(int wave, int count)
        {
            Assert.AreEqual(count, WaveCurves.ModifierCount(W, wave));
        }

        [TestCase(19, 0)]
        [TestCase(21, 1)]
        [TestCase(39, 1)]
        [TestCase(40, 0)]
        [TestCase(41, 2)]
        [TestCase(55, 2)]
        public void S07_TV_101__ModifierCount(int wave, int count)
        {
            Assert.AreEqual(count, WaveCurves.ModifierCount(W, wave));
        }

        [TestCase(1, 1000000L)]
        [TestCase(6, 1600000L)]
        [TestCase(10, 2080000L)]
        [TestCase(12, 2320000L)]
        [TestCase(13, 2459200L)]
        public void S07_TV_022__HpMicroLinearThenExponential(int wave, long micro)
        {
            Assert.AreEqual(micro, WaveCurves.HpMicro(W, wave));
        }

        [TestCase(20, 3697724L)]
        [TestCase(30, 6622055L)]
        public void S07_TV_023__HpMicroFlooredEachStep(int wave, long micro)
        {
            Assert.AreEqual(micro, WaveCurves.HpMicro(W, wave));
        }

        // 05 요약서 3.2절: H(1)=1.00, H(6)=1.60, H(10)=2.08, H(12)=2.32, H(13)=2.4592, H(20)≈3.6977, H(30)≈6.6221
        [TestCase(1, 1.00f)]
        [TestCase(6, 1.60f)]
        [TestCase(10, 2.08f)]
        [TestCase(12, 2.32f)]
        [TestCase(13, 2.4592f)]
        [TestCase(20, 3.6977f)]
        [TestCase(30, 6.6221f)]
        public void S05_HpMul__ModuleValueMatchesTable(int wave, float expected)
        {
            WaveModule module = new WaveDirector(TestContent.Db);
            Assert.AreEqual(expected, module.HpMul(wave), 0.0001f);
        }

        // 연습 방(w.Wave.Wave = 0)은 H(1)로 읽는다. 콘텐츠가 붙기 전에도 1이다
        [Test]
        public void S05_HpMul__WaveBelowOneAndUnboundAreOne()
        {
            Assert.AreEqual(1f, new WaveDirector(TestContent.Db).HpMul(0), 0.0001f);
            Assert.AreEqual(1f, new WaveDirector().HpMul(1), 0.0001f);
        }

        // slime w=1,12,20,30 → 20,46,73,132 / golem w=5 → 444
        [TestCase(Ids.Enemy.SlimeMoss, 1, 20)]
        [TestCase(Ids.Enemy.SlimeMoss, 12, 46)]
        [TestCase(Ids.Enemy.SlimeMoss, 20, 73)]
        [TestCase(Ids.Enemy.SlimeMoss, 30, 132)]
        [TestCase(Ids.Enemy.GolemMenhir, 5, 444)]
        public void S07_TV_024__EnemyHp(string species, int wave, int hp)
        {
            Assert.AreEqual(hp, WaveCurves.EnemyHp(W, TestContent.Db.Enemy(species).BaseHp, wave, false));
        }

        [Test]
        public void S07_TV_027__EliteGolemHpAtWave12()
        {
            Assert.AreEqual(2088, WaveCurves.EnemyHp(W, TestContent.Db.Enemy(Ids.Enemy.GolemMenhir).BaseHp, 12, true));
        }

        [Test]
        public void S07_TV_028__ThickHideGolemHpAtWave30()
        {
            int percent = WaveCurves.ToPercent(TestContent.Db.Modifier(Ids.Whim.ThickHide).EnemyHpMultiplier.Value);
            Assert.AreEqual(130, percent);
            Assert.AreEqual(2582, WaveCurves.EnemyHp(W, TestContent.Db.Enemy(Ids.Enemy.GolemMenhir).BaseHp, 30, false, percent));
        }

        // 05 요약서 3.2절(S6 T011~T020): 보스 체력 = 기본 × H(w) / H(10)
        [TestCase(Ids.Boss.KilnBoar, 10, 3000)]
        [TestCase(Ids.Boss.MirrorPhantom, 20, 7111)]
        [TestCase(Ids.Boss.SporeQueen, 30, 14326)]
        [TestCase(Ids.Boss.StormOwl, 40, 28507)]
        [TestCase(Ids.Boss.FrostGoat, 50, 56157)]
        [TestCase(Ids.Boss.NightSerpent, 60, 118855)]
        [TestCase(Ids.Boss.KilnBoar, 70, 98239)]
        [TestCase(Ids.Boss.MirrorPhantom, 80, 234575)]
        public void S07_TV_029__BossHp(string bossId, int wave, int hp)
        {
            var boss = TestContent.Db.Boss(bossId);
            Assert.AreEqual(hp, WaveCurves.BossHp(W, boss.BaseHp, wave, boss.HpScaleAnchorWave));
        }

        // 10 요약서 6절: w ≥ 200 극단 — 적 체력은 int 포화, 예외 없음
        [Test]
        public void S07_Edge__ExtremeWaveSaturatesInsteadOfOverflowing()
        {
            Assert.AreEqual(int.MaxValue, WaveCurves.EnemyHp(W, 300f, 5000, true));
            Assert.Greater(WaveCurves.HpMicro(W, 5000), 0L);
            Assert.GreaterOrEqual(WaveCurves.HpMicro(W, 201), WaveCurves.HpMicro(W, 200));
            Assert.AreEqual(200, WaveCurves.Budget(W, 5000));
            Assert.AreEqual(8, WaveCurves.BundleCount(W, 5000));
            Assert.AreEqual(6, WaveCurves.TelegraphCap(W, 5000));
        }

        [Test]
        public void S07_Budget__ModuleOverrideReadsCurve()
        {
            WaveModule module = new WaveDirector(TestContent.Db);
            Assert.AreEqual(82, module.Budget(12));
            Assert.AreEqual(200, module.Budget(42));
        }
    }
}
