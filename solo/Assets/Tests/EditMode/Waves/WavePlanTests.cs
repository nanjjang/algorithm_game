using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>웨이브 구성 생성. 06 요약서 3.2~3.4절, 01 요약서 7.1절, 10 요약서 6절. 기대값은 요약서(원문 S07 17절)의 벡터.</summary>
    public class WavePlanTests
    {
        const int SeedCount = 40;
        static IContentDb Db => TestContent.Db;

        // ------------------------------------------------------------ 3.3절 고정 각본

        [Test]
        public void S07_TV_080__Wave1IsSixteenSlimes()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 1);
                Assert.AreEqual(16, plan.Entries.Count);
                Assert.AreEqual(16, WaveRig.Count(plan, Ids.Enemy.SlimeMoss));
                Assert.AreEqual(16, WaveRig.Cost(plan, false));
                Assert.AreEqual(Ids.Frame.Mixed, plan.Frame);
                Assert.AreEqual(Ids.Tod.Day, plan.Tod);
            }
        }

        [Test]
        public void S07_TV_081__Wave2HasTwoOrThreeBugsRestSlime()
        {
            bool sawTwo = false, sawThree = false;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 2);
                int bugs = WaveRig.Count(plan, Ids.Enemy.BugLantern);
                Assert.IsTrue(bugs == 2 || bugs == 3, "bug " + bugs);
                sawTwo |= bugs == 2;
                sawThree |= bugs == 3;
                Assert.AreEqual(plan.Entries.Count - bugs, WaveRig.Count(plan, Ids.Enemy.SlimeMoss));
                Assert.AreEqual(22, WaveRig.Cost(plan, false));
            }
            Assert.IsTrue(sawTwo && sawThree, "2와 3이 둘 다 나와야 한다");
        }

        [Test]
        public void S07_TV_082__Wave3OneShamanEightWolvesNoSlime()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 3);
                Assert.AreEqual(1, WaveRig.Count(plan, Ids.Enemy.ShamanCap));
                Assert.AreEqual(8, WaveRig.Count(plan, Ids.Enemy.WolfBramble));
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.SlimeMoss));
                Assert.AreEqual(9, plan.Entries.Count);
                Assert.AreEqual(28, WaveRig.Cost(plan, false));
            }
        }

        // 10 요약서 6절 "예산보다 비싼 종만 남음 → 남는 예산은 이끼 슬라임": 늑대 8 뒤 남은 2는 슬라임 2
        [Test]
        public void S07_TV_083__Wave4ShamanHogEightWolvesTwoSlimes()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 4);
                Assert.AreEqual(1, WaveRig.Count(plan, Ids.Enemy.ShamanCap));
                Assert.AreEqual(1, WaveRig.Count(plan, Ids.Enemy.HogNeedle));
                Assert.AreEqual(8, WaveRig.Count(plan, Ids.Enemy.WolfBramble));
                Assert.AreEqual(2, WaveRig.Count(plan, Ids.Enemy.SlimeMoss));
                Assert.AreEqual(12, plan.Entries.Count);
                Assert.AreEqual(34, WaveRig.Cost(plan, false));
            }
        }

        // 3.3절 표: 5 golem 1, 6 golem·shaman·weasel 1, 7 bat 무리 1(6~8), 8 wisp 2, 9 bat 무리 + wolf 60% 몫, 11 vine 1
        [Test]
        public void S07_Script__Waves5To11HaveTheirFixedSpecies()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Assert.GreaterOrEqual(WaveRig.Count(WaveRig.Build(seed, 5), Ids.Enemy.GolemMenhir), 1);
                var w6 = WaveRig.Build(seed, 6);
                Assert.GreaterOrEqual(WaveRig.Count(w6, Ids.Enemy.GolemMenhir), 1);
                Assert.GreaterOrEqual(WaveRig.Count(w6, Ids.Enemy.ShamanCap), 1);
                Assert.GreaterOrEqual(WaveRig.Count(w6, Ids.Enemy.WeaselPot), 1);
                Assert.GreaterOrEqual(WaveRig.Count(WaveRig.Build(seed, 7), Ids.Enemy.BatDusk), 6);
                Assert.GreaterOrEqual(WaveRig.Count(WaveRig.Build(seed, 8), Ids.Enemy.WispStray), 2);
                var w9 = WaveRig.Build(seed, 9);
                Assert.GreaterOrEqual(WaveRig.Count(w9, Ids.Enemy.BatDusk), 6);
                // 늑대 몫 = 64 × 60% = 38 → 12마리(36). 혼합 단계는 주 묶음 밖의 종만 뽑으므로 정확히 12
                Assert.AreEqual(12, WaveRig.Count(w9, Ids.Enemy.WolfBramble));
                Assert.GreaterOrEqual(WaveRig.Count(WaveRig.Build(seed, 11), Ids.Enemy.VineThorn), 1);
            }
        }

        [Test]
        public void S07_Script__Waves1To12UseMixedFrameLabelAndNoWhim()
        {
            for (int wave = 1; wave <= 12; wave++)
            {
                var plan = WaveRig.Build(7, wave);
                if (DayCycle.IsBossWave(wave)) { Assert.IsNull(plan.Frame); continue; }
                Assert.AreEqual(Ids.Frame.Mixed, plan.Frame, "wave " + wave);
                Assert.AreEqual(0, plan.Whims.Count);
            }
        }

        [Test]
        public void S07_TV_089__Wave12HasExactlyOneEliteInsideBudget()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 12);
                int elites = 0;
                for (int i = 0; i < plan.Entries.Count; i++)
                    if (plan.Entries[i].Elite) { elites++; Assert.IsFalse(plan.Entries[i].OutsideBudget); }
                Assert.AreEqual(1, elites);
                // 엘리트 비용 ×3이 예산 82 안에서 빠진다
                Assert.AreEqual(82, WaveRig.Cost(plan, false));
            }
        }

        [Test]
        public void S07_TV_090__Wave13And15ForceMixedWithNewSpecies()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var w13 = WaveRig.Build(seed, 13);
                Assert.AreEqual(Ids.Frame.Mixed, w13.Frame);
                Assert.GreaterOrEqual(WaveRig.Count(w13, Ids.Enemy.BoarTusk), 1);
                var w15 = WaveRig.Build(seed, 15);
                Assert.AreEqual(Ids.Frame.Mixed, w15.Frame);
                Assert.GreaterOrEqual(WaveRig.Count(w15, Ids.Enemy.ToadDrum), 1);
            }
        }

        // ------------------------------------------------------------ 종 상한·시간대

        [Test]
        public void S07_TV_073__SpeciesCapsHoldForWholePlan()
        {
            var caps = new Dictionary<string, int>
            {
                { Ids.Enemy.GolemMenhir, 3 }, { Ids.Enemy.VineThorn, 4 }, { Ids.Enemy.ToadDrum, 2 },
                { Ids.Enemy.BoarTusk, 3 }, { Ids.Enemy.HogNeedle, 4 }, { Ids.Enemy.WispStray, 4 }
            };
            for (ulong seed = 1; seed <= 8; seed++)
                for (int wave = 1; wave <= 100; wave++)
                {
                    var plan = WaveRig.Build(seed, wave);
                    foreach (var kv in caps)
                    {
                        int cap = kv.Value;
                        // 긴 밤이면 도깨비불 상한 4 → 6
                        if (kv.Key == Ids.Enemy.WispStray && plan.HasWhim(Ids.Whim.LongNight)) cap = 6;
                        Assert.LessOrEqual(WaveRig.Count(plan, kv.Key), cap, kv.Key + " wave " + wave + " seed " + seed);
                    }
                }
        }

        [Test]
        public void S07_TV_073__LongNightRaisesWispCapToSix()
        {
            var whims = new List<string> { Ids.Whim.LongNight };
            Assert.AreEqual(6, WavePlanner.SpeciesCap(Db, Ids.Enemy.WispStray, whims));
            Assert.AreEqual(4, WavePlanner.SpeciesCap(Db, Ids.Enemy.WispStray, null));
            Assert.AreEqual(3, WavePlanner.SpeciesCap(Db, Ids.Enemy.GolemMenhir, whims));
            Assert.AreEqual(0, WavePlanner.SpeciesCap(Db, Ids.Enemy.SlimeMoss, null));
            for (ulong seed = 1; seed <= SeedCount; seed++)
                Assert.LessOrEqual(WaveRig.Count(WaveRig.Build(seed, 28, WaveRig.Whims(Ids.Whim.LongNight)), Ids.Enemy.WispStray), 6);
        }

        // 01 요약서 7.1절: 낮에는 박쥐·도깨비불 가중치 0
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(21)]
        [TestCase(22)]
        [TestCase(23)]
        [TestCase(24)]
        public void S07_TV_084__DayWaveHasNoBatOrWisp(int wave)
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, wave);
                Assert.AreEqual(Ids.Tod.Day, plan.Tod);
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.BatDusk));
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.WispStray));
            }
        }

        [TestCase(18)]
        [TestCase(19)]
        public void S07_TV_085__NightWaveHasNoHogVineBoar(int wave)
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, wave);
                Assert.AreEqual(Ids.Tod.Night, plan.Tod);
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.HogNeedle));
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.VineThorn));
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.BoarTusk));
            }
        }

        [TestCase(5)]
        [TestCase(6)]
        public void S07_TV_086__DuskBeforeWave7HasNoBat(int wave)
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, wave);
                Assert.AreEqual(Ids.Tod.Dusk, plan.Tod);
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.BatDusk));
                // 해질녘에는 도깨비불이 없다(가중치 0, 시간대 칸도 밤만)
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.WispStray));
            }
        }

        [Test]
        public void S07_TV_087__DuskWeights()
        {
            var t = WaveTuning.From(Db);
            Assert.AreEqual(Ids.Tod.Dusk, WavePlanner.TodOf(Db, 17));
            Assert.AreEqual(200, WavePlanner.Weight(Db, t, Ids.Enemy.WolfBramble, Ids.Tod.Dusk, null));
            Assert.AreEqual(50, WavePlanner.Weight(Db, t, Ids.Enemy.BatDusk, Ids.Tod.Dusk, null));
            Assert.AreEqual(0, WavePlanner.Weight(Db, t, Ids.Enemy.WispStray, Ids.Tod.Dusk, null));
            Assert.AreEqual(100, WavePlanner.Weight(Db, t, Ids.Enemy.SlimeMoss, Ids.Tod.Dusk, null));
        }

        [Test]
        public void S07_TV_088__NightWithLongNightWeights()
        {
            var t = WaveTuning.From(Db);
            var whims = new List<string> { Ids.Whim.LongNight };
            Assert.AreEqual(Ids.Tod.Night, WavePlanner.TodOf(Db, 28));
            Assert.AreEqual(400, WavePlanner.Weight(Db, t, Ids.Enemy.BatDusk, Ids.Tod.Night, whims));
            Assert.AreEqual(400, WavePlanner.Weight(Db, t, Ids.Enemy.WispStray, Ids.Tod.Night, whims));
            Assert.AreEqual(50, WavePlanner.Weight(Db, t, Ids.Enemy.SlimeMoss, Ids.Tod.Night, whims));
            // 긴 밤이 없으면 밤의 박쥐·도깨비불은 200
            Assert.AreEqual(200, WavePlanner.Weight(Db, t, Ids.Enemy.BatDusk, Ids.Tod.Night, null));
        }

        // 01 요약서 7.1절 가중치 표(낮)
        [Test]
        public void S01_TodWeights__DayTable()
        {
            var t = WaveTuning.From(Db);
            Assert.AreEqual(150, WavePlanner.Weight(Db, t, Ids.Enemy.HogNeedle, Ids.Tod.Day, null));
            Assert.AreEqual(150, WavePlanner.Weight(Db, t, Ids.Enemy.VineThorn, Ids.Tod.Day, null));
            Assert.AreEqual(150, WavePlanner.Weight(Db, t, Ids.Enemy.BoarTusk, Ids.Tod.Day, null));
            Assert.AreEqual(0, WavePlanner.Weight(Db, t, Ids.Enemy.BatDusk, Ids.Tod.Day, null));
            Assert.AreEqual(0, WavePlanner.Weight(Db, t, Ids.Enemy.WispStray, Ids.Tod.Day, null));
            Assert.AreEqual(0, WavePlanner.Weight(Db, t, Ids.Enemy.HogNeedle, Ids.Tod.Night, null));
            Assert.IsFalse(WavePlanner.IsAvailable(Db, Db.Enemy(Ids.Enemy.BatDusk), 21, Ids.Tod.Day));
            Assert.IsFalse(WavePlanner.IsAvailable(Db, Db.Enemy(Ids.Enemy.SporeSeedling), 50, Ids.Tod.Day));
            Assert.IsFalse(WavePlanner.IsAvailable(Db, Db.Enemy(Ids.Enemy.BoarTusk), 12, Ids.Tod.Day));
            Assert.IsTrue(WavePlanner.IsAvailable(Db, Db.Enemy(Ids.Enemy.BoarTusk), 13, Ids.Tod.Day));
        }

        // ------------------------------------------------------------ 틀

        [Test]
        public void S07_TV_091__FrameAfterSwarmIsNotSwarm()
        {
            int found = 0;
            for (ulong seed = 1; seed <= 400; seed++)
            {
                if (WavePlanner.FrameOf(Db, seed, 16) != Ids.Frame.Swarm) continue;
                found++;
                Assert.AreNotEqual(Ids.Frame.Swarm, WavePlanner.FrameOf(Db, seed, 17), "seed " + seed);
                Assert.AreEqual(Ids.Frame.Swarm, WaveRig.Build(seed, 16).Frame);
                Assert.AreNotEqual(Ids.Frame.Swarm, WaveRig.Build(seed, 17).Frame);
            }
            Assert.Greater(found, 0, "웨이브 16이 떼인 씨앗이 있어야 한다");
        }

        [Test]
        public void S07_TV_092__FrameAfterBossLooksAtWaveBeforeBoss()
        {
            int found = 0;
            for (ulong seed = 1; seed <= 400; seed++)
            {
                if (WavePlanner.FrameOf(Db, seed, 19) != Ids.Frame.Battery) continue;
                found++;
                Assert.IsNull(WavePlanner.FrameOf(Db, seed, 20));
                Assert.AreNotEqual(Ids.Frame.Battery, WavePlanner.FrameOf(Db, seed, 21), "seed " + seed);
            }
            Assert.Greater(found, 0, "웨이브 19가 포대인 씨앗이 있어야 한다");
        }

        [Test]
        public void S07_Frame__NeverRepeatsPreviousNormalWaveAndCoversAllFive()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                string prev = null;
                for (int wave = 13; wave <= 70; wave++)
                {
                    if (DayCycle.IsBossWave(wave)) continue;
                    string f = WavePlanner.FrameOf(Db, seed, wave);
                    Assert.IsNotNull(Db.Frame(f), "wave " + wave);
                    if (wave == 13 || wave == 15) Assert.AreEqual(Ids.Frame.Mixed, f);
                    else Assert.AreNotEqual(prev, f, "seed " + seed + " wave " + wave);
                    // 웨이브 14는 직전(13)이 혼합이라 혼합이 아니다
                    if (wave == 14 || wave == 16) Assert.AreNotEqual(Ids.Frame.Mixed, f);
                    seen.Add(f);
                    prev = f;
                }
            }
            Assert.AreEqual(5, seen.Count);
        }

        // 사냥대의 고정 북두꺼비는 첫 등장(15) 전에는 빠진다(시간대·웨이브에 없는 종은 틀에서 빠진다)
        [Test]
        public void S07_Frame__UnavailableFixedSpeciesIsDropped()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 14, new PlanOverrides { Frame = Ids.Frame.HuntingParty });
                Assert.AreEqual(0, WaveRig.Count(plan, Ids.Enemy.ToadDrum));
                Assert.AreEqual(90, WaveRig.Cost(plan, false));
                var later = WaveRig.Build(seed, 16, new PlanOverrides { Frame = Ids.Frame.HuntingParty });
                Assert.GreaterOrEqual(WaveRig.Count(later, Ids.Enemy.ToadDrum), 1);
            }
        }

        // 포대: 선돌지기 1~2 고정
        [Test]
        public void S07_Frame__BatteryHasOneOrTwoGolemsAtLeast()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
                Assert.GreaterOrEqual(WaveRig.Count(WaveRig.Build(seed, 14, new PlanOverrides { Frame = Ids.Frame.Battery }), Ids.Enemy.GolemMenhir), 1);
        }

        // ------------------------------------------------------------ 결정론·예산

        [Test]
        public void S07_TV_093__SameSeedSamePlan()
        {
            for (int wave = 1; wave <= 60; wave++)
            {
                var a = WaveRig.Build(20260101, wave);
                var b = WaveRig.Build(20260101, wave);
                Assert.AreEqual(a.Tod, b.Tod);
                Assert.AreEqual(a.Frame, b.Frame);
                Assert.AreEqual(a.BossId, b.BossId);
                Assert.AreEqual(a.Budget, b.Budget);
                Assert.AreEqual(a.BundleCount, b.BundleCount);
                CollectionAssert.AreEqual(a.Whims, b.Whims);
                CollectionAssert.AreEqual(a.PreviewSpecies, b.PreviewSpecies);
                Assert.AreEqual(a.Entries.Count, b.Entries.Count, "wave " + wave);
                for (int i = 0; i < a.Entries.Count; i++)
                {
                    Assert.AreEqual(a.Entries[i].Species, b.Entries[i].Species);
                    Assert.AreEqual(a.Entries[i].Elite, b.Entries[i].Elite);
                    Assert.AreEqual(a.Entries[i].OutsideBudget, b.Entries[i].OutsideBudget);
                    Assert.AreEqual(a.Entries[i].Bundle, b.Entries[i].Bundle);
                }
            }
        }

        [Test]
        public void S07_TV_093__ModuleBuildPlanIsRepeatableAndMatchesPlanner()
        {
            var rig = WaveRig.Create(77);
            for (int wave = 1; wave <= 30; wave++)
            {
                var a = rig.Director.BuildPlan(rig.World, wave);
                var b = rig.Director.BuildPlan(rig.World, wave);
                var c = WaveRig.Build(77, wave);
                Assert.AreEqual(c.Entries.Count, a.Entries.Count);
                Assert.AreEqual(a.Entries.Count, b.Entries.Count);
                for (int i = 0; i < a.Entries.Count; i++)
                {
                    Assert.AreEqual(a.Entries[i].Species, b.Entries[i].Species);
                    Assert.AreEqual(c.Entries[i].Species, a.Entries[i].Species);
                    Assert.AreEqual(c.Entries[i].Bundle, a.Entries[i].Bundle);
                }
            }
        }

        [Test]
        public void S07_TV_094__NormalWaveCostEqualsBudgetExactly()
        {
            for (ulong seed = 1; seed <= 10; seed++)
                for (int wave = 1; wave <= 100; wave++)
                {
                    if (DayCycle.IsBossWave(wave)) continue;
                    var trace = new PlanTrace();
                    var plan = WaveRig.Build(seed, wave, null, trace);
                    int budget = WaveCurves.Budget(Db.Waves, wave);
                    // 예산 안 엘리트 포함, 예산 밖 제외. 큰 무리면 추가분만큼 더
                    Assert.AreEqual(budget + trace.ExtraBudget, WaveRig.Cost(plan, false), "seed " + seed + " wave " + wave);
                    Assert.AreEqual(budget + trace.ExtraBudget, plan.Budget);
                    if (!plan.HasWhim(Ids.Whim.BigPack)) Assert.AreEqual(0, trace.ExtraBudget);
                }
        }

        // 10 요약서 6절: w ≥ 42는 예산 200 고정
        [TestCase(42)]
        [TestCase(77)]
        [TestCase(199)]
        public void S07_Edge__BudgetStaysAt200FromWave42(int wave)
        {
            for (ulong seed = 1; seed <= 6; seed++)
                Assert.AreEqual(200, WaveRig.Cost(WaveRig.Build(seed, wave, WaveRig.Whims()), false));
        }

        [Test]
        public void S07_TV_095__BigPackAddsThirtyAtWave22WithCheapSpeciesOnly()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var trace = new PlanTrace();
                var plan = WaveRig.Build(seed, 22, WaveRig.Whims(Ids.Whim.BigPack), trace);
                Assert.AreEqual(122, trace.BaseBudget);
                Assert.AreEqual(30, trace.ExtraBudget);
                Assert.AreEqual(152, WaveRig.Cost(plan, false));
                int extraCost = 0;
                Assert.Greater(trace.ExtraEntryCount, 0);
                for (int i = trace.ExtraFirstEntry; i < trace.ExtraFirstEntry + trace.ExtraEntryCount; i++)
                {
                    var entry = plan.Entries[i];
                    Assert.IsFalse(entry.Elite);
                    Assert.IsFalse(entry.OutsideBudget);
                    Assert.LessOrEqual(Db.Enemy(entry.Species).Cost, 2, entry.Species);
                    extraCost += Db.Enemy(entry.Species).Cost;
                }
                Assert.AreEqual(30, extraCost);
            }
        }

        [Test]
        public void S07_TV_067__ChiefParadeElitesAreOutsideBudget()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var plan = WaveRig.Build(seed, 25, WaveRig.Whims(Ids.Whim.ChiefParade));
                int inside = 0, outside = 0;
                for (int i = 0; i < plan.Entries.Count; i++)
                {
                    if (!plan.Entries[i].Elite) { Assert.IsFalse(plan.Entries[i].OutsideBudget); continue; }
                    if (plan.Entries[i].OutsideBudget) outside++; else inside++;
                }
                Assert.AreEqual(2, inside);
                Assert.AreEqual(2, outside);
                Assert.AreEqual(134, WaveRig.Cost(plan, false));
            }
        }

        [TestCase(10, Ids.Boss.KilnBoar)]
        [TestCase(20, Ids.Boss.MirrorPhantom)]
        [TestCase(30, Ids.Boss.SporeQueen)]
        [TestCase(40, Ids.Boss.StormOwl)]
        [TestCase(50, Ids.Boss.FrostGoat)]
        [TestCase(60, Ids.Boss.NightSerpent)]
        [TestCase(70, Ids.Boss.KilnBoar)]
        public void S07_TV_096__BossWaveHasNoEnemiesNoWhims(int wave, string bossId)
        {
            var plan = WaveRig.Build(5, wave);
            Assert.AreEqual(bossId, plan.BossId);
            Assert.AreEqual(0, plan.Entries.Count);
            Assert.AreEqual(0, plan.Whims.Count);
            Assert.AreEqual(0, plan.PreviewSpecies.Count);
            Assert.AreEqual(Ids.Tod.Boss, plan.Tod);
            Assert.IsFalse(plan.PhantomFirst);
        }

        sealed class FixedBoss : BossModule
        {
            public int Calls;
            public bool LastPhantomFirst;
            public override string SelectBoss(IContentDb db, int wave, bool phantomFirst)
            {
                Calls++;
                LastPhantomFirst = phantomFirst;
                return phantomFirst ? Ids.Boss.MirrorPhantom : Ids.Boss.FrostGoat;
            }
        }

        // 보스 고르기는 Bosses.SelectBoss에 맡긴다. 첫 만남은 판 설정이 켜져 있고 첫 보스 웨이브(10)일 때만
        [Test]
        public void S07_Boss__PlanAsksBossModuleAndCarriesPhantomFirst()
        {
            var rig = WaveRig.Create(3);
            var bosses = new FixedBoss();
            rig.World.Mods.Bosses = bosses;
            rig.World.Run.Config = new RunConfig { PhantomFirst = true };

            var w10 = rig.Director.BuildPlan(rig.World, 10);
            Assert.IsTrue(w10.PhantomFirst);
            Assert.IsTrue(bosses.LastPhantomFirst);
            Assert.AreEqual(Ids.Boss.MirrorPhantom, w10.BossId);

            var w20 = rig.Director.BuildPlan(rig.World, 20);
            Assert.IsFalse(w20.PhantomFirst);
            Assert.AreEqual(Ids.Boss.FrostGoat, w20.BossId);

            rig.World.Run.PhantomFirstDone = true;
            Assert.IsFalse(rig.Director.BuildPlan(rig.World, 10).PhantomFirst);
            Assert.AreEqual(3, bosses.Calls);
        }

        // ------------------------------------------------------------ 3.4절 변덕

        [Test]
        public void S07_TV_101__PlanWhimCountFollowsCurve()
        {
            for (ulong seed = 1; seed <= 10; seed++)
            {
                Assert.AreEqual(0, WaveRig.Build(seed, 19).Whims.Count);
                Assert.AreEqual(0, WaveRig.Build(seed, 20).Whims.Count);
                Assert.AreEqual(1, WaveRig.Build(seed, 21).Whims.Count);
                Assert.AreEqual(1, WaveRig.Build(seed, 39).Whims.Count);
                Assert.AreEqual(0, WaveRig.Build(seed, 40).Whims.Count);
                Assert.AreEqual(2, WaveRig.Build(seed, 41).Whims.Count);
                Assert.AreEqual(2, WaveRig.Build(seed, 55).Whims.Count);
            }
        }

        [Test]
        public void S07_TV_102__LongNightOnlyAtNight()
        {
            var day = WavePlanner.WhimPool(Db, Ids.Tod.Day);
            var dusk = WavePlanner.WhimPool(Db, Ids.Tod.Dusk);
            var night = WavePlanner.WhimPool(Db, Ids.Tod.Night);
            CollectionAssert.DoesNotContain(day, Ids.Whim.LongNight);
            CollectionAssert.DoesNotContain(dusk, Ids.Whim.LongNight);
            CollectionAssert.Contains(night, Ids.Whim.LongNight);
            Assert.AreEqual(7, day.Count);
            Assert.AreEqual(8, night.Count);
            // 데이터 순서 그대로
            CollectionAssert.AreEqual(new[]
            {
                Ids.Whim.Crowding, Ids.Whim.ThickHide, Ids.Whim.BigPack, Ids.Whim.ChiefParade,
                Ids.Whim.LastBreath, Ids.Whim.SlowBarrage, Ids.Whim.LongNight, Ids.Whim.NarrowField
            }, night);

            bool sawLongNight = false;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                CollectionAssert.DoesNotContain(WavePlanner.PickWhims(Db, seed, 22), Ids.Whim.LongNight);
                CollectionAssert.DoesNotContain(WavePlanner.PickWhims(Db, seed, 25), Ids.Whim.LongNight);
                sawLongNight |= WavePlanner.PickWhims(Db, seed, 28).Contains(Ids.Whim.LongNight);
            }
            Assert.IsTrue(sawLongNight, "밤 웨이브에서는 긴 밤이 뽑힐 수 있어야 한다");
        }

        [Test]
        public void S07_TV_103__TwoWhimsAreDistinct()
        {
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var whims = WavePlanner.PickWhims(Db, seed, 45);
                Assert.AreEqual(2, whims.Count);
                Assert.AreNotEqual(whims[0], whims[1], "seed " + seed);
            }
        }

        [Test]
        public void S07_TV_104__WhimsDependOnlyOnSeedAndWave()
        {
            for (int wave = 21; wave <= 100; wave++)
            {
                var alone = WavePlanner.PickWhims(Db, 9001, wave);
                // 구성 갈래를 다르게 돌려도(틀 강제) 변덕은 같다
                var viaPlan = WaveRig.Build(9001, wave, new PlanOverrides { Frame = Ids.Frame.Mixed }).Whims;
                var viaPlan2 = WaveRig.Build(9001, wave).Whims;
                CollectionAssert.AreEqual(alone, viaPlan, "wave " + wave);
                CollectionAssert.AreEqual(alone, viaPlan2, "wave " + wave);
            }
        }

        [Test]
        public void S07_Whims__EveryWhimCanBePickedAndAllComeFromData()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 300; seed++)
                foreach (var id in WavePlanner.PickWhims(Db, seed, 48)) { Assert.IsNotNull(Db.Modifier(id)); seen.Add(id); }
            Assert.AreEqual(8, seen.Count);
        }

        // ------------------------------------------------------------ 묶음·미리보기

        [Test]
        public void S07_Bundles__IndicesInRangeAndElitesStartFromLastBundle()
        {
            for (ulong seed = 1; seed <= 10; seed++)
                for (int wave = 1; wave <= 60; wave++)
                {
                    if (DayCycle.IsBossWave(wave)) continue;
                    var plan = WaveRig.Build(seed, wave);
                    Assert.AreEqual(WaveCurves.BundleCount(Db.Waves, wave), plan.BundleCount);
                    int elites = 0;
                    for (int i = 0; i < plan.Entries.Count; i++)
                    {
                        Assert.GreaterOrEqual(plan.Entries[i].Bundle, 0);
                        Assert.Less(plan.Entries[i].Bundle, plan.BundleCount);
                        if (!plan.Entries[i].Elite) continue;
                        // 엘리트는 마지막 묶음부터 거꾸로 하나씩
                        Assert.AreEqual(plan.BundleCount - 1 - elites % plan.BundleCount, plan.Entries[i].Bundle);
                        elites++;
                    }
                    bool firstBundleUsed = false;
                    for (int i = 0; i < plan.Entries.Count; i++) firstBundleUsed |= plan.Entries[i].Bundle == 0;
                    Assert.IsTrue(firstBundleUsed, "첫 묶음은 비지 않는다");
                }
        }

        [Test]
        public void S07_Preview__DistinctSpeciesOfThePlan()
        {
            var plan = WaveRig.Build(11, 26);
            CollectionAssert.AllItemsAreUnique(plan.PreviewSpecies);
            for (int i = 0; i < plan.Entries.Count; i++) CollectionAssert.Contains(plan.PreviewSpecies, plan.Entries[i].Species);
            foreach (var id in plan.PreviewSpecies) Assert.Greater(WaveRig.Count(plan, id), 0);
            CollectionAssert.AreEqual(new[] { Ids.Enemy.SlimeMoss }, WaveRig.Build(11, 1).PreviewSpecies);
        }

        // 웨이브 1~4에 나오는 종은 비전 저항이 아니다(시작 블록 마력탄이 비전)
        [Test]
        public void S07_TV_097__EarlySpeciesDoNotResistArcane()
        {
            for (ulong seed = 1; seed <= 10; seed++)
                for (int wave = 1; wave <= 4; wave++)
                    foreach (var id in WaveRig.Build(seed, wave).PreviewSpecies)
                        Assert.AreNotEqual(Ids.Element.Arcane, Db.Enemy(id).Resist, id);
        }
    }
}
