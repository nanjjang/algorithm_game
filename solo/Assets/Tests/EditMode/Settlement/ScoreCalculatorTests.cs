using System.IO;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Score;

namespace Wizard.Solo.Tests.Settlement
{
    /// <summary>점수 규칙(01 요약서 6절, 벡터 = S09 19.2절 T020~T040). 기대값은 요약서의 표에서 가져왔다.</summary>
    public class ScoreCalculatorTests
    {
        const int Late = 100000;   // 빠른 처리 보너스가 없는 걸린 틱

        static IContentDb Db => TestContent.Db;
        static readonly ScoreCalculator Calc = new ScoreCalculator();

        /// <summary>요약서 6.3절의 예산 식을 그대로 적은 것(구현의 Budget을 부르지 않는다).</summary>
        static int SpecBudget(int w) => w <= 12 ? 10 + 6 * w : System.Math.Min(200, 82 + 4 * (w - 12));

        static WaveStats Stats(int wave, int killX2 = 0, bool bossKilled = false, int ticks = Late, bool noHit = false, int grazes = 0)
        {
            return new WaveStats
            {
                Wave = wave, IsBoss = wave % 10 == 0, KillScoreX2 = killX2, BossKilled = bossKilled,
                ElapsedTicks = ticks, NoHit = noHit, Grazes = grazes
            };
        }

        static int Score(WaveStats s) => Calc.WaveScore(Db, s, out _);

        // T020~T024: 건너뛴 10/20/30/40/50개의 SkipScore 합
        [TestCase(10, 8398)]
        [TestCase(20, 28859)]
        [TestCase(30, 64764)]
        [TestCase(40, 120409)]
        [TestCase(50, 195829)]
        public void S09_T020_T024__SkippedScoreSum(int skip, int expected)
        {
            int sum = 0;
            for (int w = 1; w <= skip; w++) sum += Calc.SkipScore(Db, w);
            Assert.AreEqual(expected, sum);
        }

        // T025~T029: 1~n 직접(처치 B × 10 + 클리어 200, 보스 3200)
        [TestCase(10, 14000)]
        [TestCase(20, 48104)]
        [TestCase(30, 107948)]
        [TestCase(40, 200692)]
        [TestCase(50, 326392)]
        public void S09_T025_T029__DirectBaselineSum(int n, int expected)
        {
            int sum = 0;
            for (int w = 1; w <= n; w++)
            {
                bool boss = w % 10 == 0;
                // 처치 원점수 B × 10 → 2배 정수로 든다. 보스 웨이브는 보스 처치 3000 + 클리어 200.
                sum += Score(Stats(w, killX2: boss ? 0 : SpecBudget(w) * 10 * 2, bossKilled: boss));
            }
            Assert.AreEqual(expected, sum);
        }

        [Test]
        public void S09_T030__SkipScoreWave2()
        {
            Assert.AreEqual(277, Calc.SkipScore(Db, 2));
        }

        [Test]
        public void S09_T031__SkipScoreWave10()
        {
            Assert.AreEqual(3648, Calc.SkipScore(Db, 10));
        }

        [Test]
        public void S09_T032__PhantomFirstWave10IsFixed()
        {
            // 첫 만남 중의 처치·스침·무피격·빠른 처리는 점수가 없다.
            var s = Stats(10, killX2: 400, bossKilled: false, ticks: 600, noHit: true, grazes: 30);
            s.PhantomFirst = true;
            int total = Calc.WaveScore(Db, s, out ScoreBreakdown b);
            Assert.AreEqual(3648, total);
            Assert.AreEqual(3648, b.PhantomFirst);
            Assert.AreEqual(3648, b.Total);
            Assert.AreEqual(0, b.Kills + b.Boss + b.Clear + b.Fast + b.NoHit + b.Graze + b.Variance);
        }

        [Test]
        public void S09_T032__PhantomFirstFlagOnOtherWaveIsNormal()
        {
            // phantom_first인 판이라도 웨이브 10이 아니면 보통 점수다(클리어 200 × S(1)).
            var s = Stats(1);
            s.PhantomFirst = true;
            Assert.AreEqual(200, Score(s));
        }

        [Test]
        public void S09_T033__KilnBoarWave10NoBonus()
        {
            int total = Calc.WaveScore(Db, Stats(10, bossKilled: true), out ScoreBreakdown b);
            Assert.AreEqual(6080, total);
            Assert.AreEqual(5700, b.Boss);    // 3000 × 1.9
            Assert.AreEqual(380, b.Clear);    // 200 × 1.9
        }

        [Test]
        public void S09_T034__FastClear()
        {
            // 목표 3,600틱, 걸린 3,000틱, w = 1 → 빠른 처리 20
            Calc.WaveScore(Db, Stats(1, ticks: 3000), out ScoreBreakdown b);
            Assert.AreEqual(20, b.Fast);
            Assert.AreEqual(220, b.Total);
            // 목표를 넘기면 0(음수가 되지 않는다)
            Calc.WaveScore(Db, Stats(1, ticks: 3601), out b);
            Assert.AreEqual(0, b.Fast);
            // 보스 웨이브의 목표는 7,200틱: 6,600틱에 끝내면 20 × S(10) = 38
            Calc.WaveScore(Db, Stats(10, bossKilled: true, ticks: 6600), out b);
            Assert.AreEqual(38, b.Fast);
        }

        [Test]
        public void S09_T036__GrazeCapNormal()
        {
            // 일반 스침 25회 → 20회만 = 100 × S
            Calc.WaveScore(Db, Stats(1, grazes: 25), out ScoreBreakdown b);
            Assert.AreEqual(100, b.Graze);
            Calc.WaveScore(Db, Stats(11, grazes: 25), out b);
            Assert.AreEqual(200, b.Graze);
            Calc.WaveScore(Db, Stats(1, grazes: 20), out ScoreBreakdown atCap);
            Assert.AreEqual(100, atCap.Graze);
        }

        [Test]
        public void S09_T037__GrazeCapBoss()
        {
            // 보스 스침 45회 → 40회만 = 200 × S(10) = 380
            Calc.WaveScore(Db, Stats(10, bossKilled: true, grazes: 45), out ScoreBreakdown b);
            Assert.AreEqual(380, b.Graze);
            Assert.AreEqual(6080 + 380, b.Total);
        }

        [Test]
        public void S09_T038__KillAfterStallIsHalf()
        {
            // 7,200틱 이후 처치(비용 4) → 20 × S. Run이 지체 뒤 처치를 cost × 10 × 1로 든다(2배 정수).
            Calc.WaveScore(Db, Stats(1, killX2: 4 * 10 * 1), out ScoreBreakdown b);
            Assert.AreEqual(20, b.Kills);
            Calc.WaveScore(Db, Stats(11, killX2: 4 * 10 * 1), out b);
            Assert.AreEqual(40, b.Kills);
            // 지체 전이면 40 × S
            Calc.WaveScore(Db, Stats(1, killX2: 4 * 10 * 2), out b);
            Assert.AreEqual(40, b.Kills);
        }

        [Test]
        public void S09_T039__WhimMultiplier125()
        {
            // 합 1000(w31: (처치 50 + 클리어 200) × 4.0), × 1.25 → 1,250
            var s = Stats(31, killX2: 100);
            Assert.AreEqual(1000, Score(s));
            s.Whims.Add(Ids.Whim.ChiefParade);
            int total = Calc.WaveScore(Db, s, out ScoreBreakdown b);
            Assert.AreEqual(1250, total);
            Assert.AreEqual(250, b.Variance);
        }

        [Test]
        public void S09_T040__TwoWhimsInOrder()
        {
            // 합 1000, × 1.15 → × 1.2 = 1,380
            var s = Stats(31, killX2: 100);
            s.Whims.Add(Ids.Whim.Crowding);
            s.Whims.Add(Ids.Whim.LastBreath);
            int total = Calc.WaveScore(Db, s, out ScoreBreakdown b);
            Assert.AreEqual(1380, total);
            Assert.AreEqual(380, b.Variance);
        }

        [Test]
        public void S09_NoHit__HundredTimesS()
        {
            Calc.WaveScore(Db, Stats(1, noHit: true), out ScoreBreakdown b);
            Assert.AreEqual(100, b.NoHit);
            Assert.AreEqual(300, b.Total);
        }

        [Test]
        public void Breakdown__LinesSumToTotal()
        {
            // 줄마다 내림이 생기는 값(홀수 처치, 홀수 스침, 어중간한 틱)에서도 줄의 합 = 합계.
            for (int w = 1; w <= 45; w++)
            {
                var s = Stats(w, killX2: 30 * w + 10, bossKilled: w % 10 == 0, ticks: 1234 + w, noHit: w % 2 == 0, grazes: w % 7);
                if (w > 20 && w % 10 != 0) s.Whims.Add(Ids.Whim.ThickHide);
                int total = Calc.WaveScore(Db, s, out ScoreBreakdown b);
                Assert.AreEqual(total, b.Total, "wave " + w);
                Assert.AreEqual(total, b.Kills + b.Boss + b.Clear + b.Fast + b.NoHit + b.Graze + b.Variance + b.PhantomFirst, "wave " + w);
            }
        }

        [Test]
        public void Factory__DefaultSetUsesScoreCalculator()
        {
            Assert.IsInstanceOf<ScoreCalculator>(SimModuleSet.CreateDefault().Score);
        }

        [Test]
        public void Contract__ScoreFolderDoesNotReferenceMeta()
        {
            // 시뮬레이션 쪽 점수 규칙은 메타 형에 기대지 않는다(00 문서 2.2절).
            string dir = Path.Combine(TestContent.AssetsDir, "Scripts", "Sim", "Score");
            string[] files = Directory.GetFiles(dir, "*.cs");
            Assert.IsNotEmpty(files);
            foreach (string f in files)
                StringAssert.DoesNotContain("Wizard.Solo.Meta", File.ReadAllText(f), f);
        }
    }
}
