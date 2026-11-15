using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>
    /// 떼 출현(솔로 조정, Resources/DataSolo/G09.json "horde" 절). 명세 값이 아니라 사용자가 정한 조정이라
    /// 기대값은 명세 구성(WavePlanner, 손잡이 꺼짐)과 JSON의 손잡이 값에서 센다. 명세 벡터 테스트는 손잡이를 끈 채로 돈다(WaveRig 기본).
    /// </summary>
    public class WaveHordeTests
    {
        static IContentDb Db => TestContent.Db;
        static WaveTuning Horde => WaveTuning.From(Db, true);
        /// <summary>보상·진행 시험에 쓰는 웨이브: 늘린 적이 있어야 한다(G09 곡선에서 웨이브 1은 배율 1.0이라 늘린 적이 없다).</summary>
        const int HordeWave = 3;

        static bool IsNormal(WavePlan plan, in WaveEntry e) =>
            !e.Elite && !e.OutsideBudget && WaveHorde.IsNormalSpecies(Db, Horde, Db.Enemy(e.Species), plan.Whims);

        static int CountNormal(WavePlan plan)
        {
            int n = 0;
            for (int i = 0; i < plan.Entries.Count; i++) if (IsNormal(plan, plan.Entries[i])) n++;
            return n;
        }

        static int CountExtras(WavePlan plan)
        {
            int n = 0;
            for (int i = 0; i < plan.Entries.Count; i++) if (WaveHorde.IsExtra(plan.Entries[i])) n++;
            return n;
        }

        // ------------------------------------------------------------ 손잡이 읽기

        [Test]
        public void Horde_Json__KnobsAreReadAndSpecTuningIgnoresThem()
        {
            var on = WaveTuning.From(Db, true);
            Assert.IsTrue(on.HordeEnabled, "G09.json horde.enabled");
            Assert.AreEqual(2.5f, on.HordeNormalCountMultiplier, 0.0001f, "곡선이 없을 때의 값(곡선이 먼저)");
            Assert.AreEqual(2f, on.HordeMaxAliveMultiplier, 0.0001f);
            Assert.AreEqual(6, on.HordePackSizeMin);
            Assert.AreEqual(12, on.HordePackSizeMax);
            Assert.AreEqual(2.5f, on.HordePackScatterRadius, 0.0001f);
            // 재미 조정(Docs/11-fun-pass.md C절): 보상 나누기, 웨이브 곡선 1.6 → 2.5, 늑대(evasive_charger) 상한 1.5, 빠른 처리 목표 보정
            Assert.IsFalse(on.HordeKeepRewardTotal, "split은 늘린 적도 보상(나눠서)");
            Assert.IsTrue(on.HordeSplitReward, "기본은 나누기(split)");
            Assert.IsTrue(on.HordeScaleFastClearTarget);
            // QA 조정(G09 _horde_note, Docs/11-fun-pass.md 1절 마지막 줄): 첫날 앞쪽을 낮춘 곡선 1.0 → 1.2 → 1.35 → 1.5 → 1.8
            Assert.AreEqual(1.0f, on.HordeMultiplier(1, "swarm"), 0.0001f, "웨이브 1은 명세 구성 그대로");
            Assert.AreEqual(1.2f, on.HordeMultiplier(2, "swarm"), 0.0001f);
            Assert.AreEqual(1.35f, on.HordeMultiplier(3, "swarm"), 0.0001f);
            Assert.AreEqual(1.425f, on.HordeMultiplier(4, "swarm"), 0.0001f, "1.35 → 1.5 사이 직선");
            Assert.AreEqual(1.5f, on.HordeMultiplier(5, "swarm"), 0.0001f);
            Assert.AreEqual(1.8f, on.HordeMultiplier(8, "swarm"), 0.0001f);
            Assert.AreEqual(1.8f, on.HordeMultiplier(12, "swarm"), 0.0001f, "8~15 평탄(성장 곡선 F절)");
            Assert.AreEqual(2.5f, on.HordeMultiplier(30, "swarm"), 0.0001f);
            Assert.AreEqual(2.5f, on.HordeMultiplier(99, "swarm"), 0.0001f, "곡선 끝 밖은 끝 값");
            Assert.AreEqual(1.5f, on.HordeMultiplier(30, "evasive_charger"), 0.0001f, "늑대 상한");
            Assert.AreEqual(1.35f, on.HordeMultiplier(3, "evasive_charger"), 0.0001f, "곡선이 상한보다 낮으면 곡선");
            Assert.AreEqual(1.5f, on.HordeMultiplier(8, "evasive_charger"), 0.0001f);
            // 무리 크기 계단: 1~3 = 3~5, 4~7 = 4~8, 8 이후 = 6~12(pack_size_min·max는 계단이 없을 때의 값)
            int lo, hi;
            on.PackSizeRange(1, out lo, out hi); Assert.AreEqual(3, lo); Assert.AreEqual(5, hi);
            on.PackSizeRange(3, out lo, out hi); Assert.AreEqual(3, lo); Assert.AreEqual(5, hi);
            on.PackSizeRange(4, out lo, out hi); Assert.AreEqual(4, lo); Assert.AreEqual(8, hi);
            on.PackSizeRange(7, out lo, out hi); Assert.AreEqual(4, lo); Assert.AreEqual(8, hi);
            on.PackSizeRange(8, out lo, out hi); Assert.AreEqual(6, lo); Assert.AreEqual(12, hi);
            on.PackSizeRange(30, out lo, out hi); Assert.AreEqual(6, lo); Assert.AreEqual(12, hi);
            CollectionAssert.Contains(on.HordeNormalRoles, "swarm");
            Assert.IsTrue(on.HordeMultiplies);
            Assert.IsTrue(on.HordePacks);

            var spec = WaveTuning.From(Db, false);
            Assert.IsFalse(spec.HordeEnabled);
            Assert.IsFalse(spec.HordeMultiplies);
            Assert.IsFalse(spec.HordePacks);
            Assert.AreEqual(1f, spec.HordeMultiplier(30, "swarm"), 0.0001f);
            Assert.AreEqual(Db.Waves.MaxAlive, WaveHorde.MaxAlive(spec, Db.Waves.MaxAlive));
            Assert.AreEqual(Db.Waves.MaxAlive * 2, WaveHorde.MaxAlive(on, Db.Waves.MaxAlive));
            // "waves" 절(명세 값)은 둘이 같다
            Assert.AreEqual(on.SpeciesBaseWeight, spec.SpeciesBaseWeight);
            Assert.AreEqual(on.SpawnRetryCount, spec.SpawnRetryCount);
        }

        [Test]
        public void Horde_Json__RewardModeFullAndOutOfRangeValuesClampToSpec()
        {
            var t = new WaveTuning();
            WaveTuning.ReadHorde(Newtonsoft.Json.Linq.JObject.Parse(
                "{\"enabled\":true,\"normal_count_multiplier\":0.5,\"max_alive_multiplier\":0.1,\"pack_size_min\":5,\"pack_size_max\":2,\"reward_mode\":\"full\"}"), t);
            Assert.IsTrue(t.HordeEnabled);
            Assert.AreEqual(1f, t.HordeNormalCountMultiplier, 0.0001f, "배율은 1 아래로 내려가지 않는다");
            Assert.AreEqual(1f, t.HordeMaxAliveMultiplier, 0.0001f);
            Assert.AreEqual(5, t.HordePackSizeMin);
            Assert.AreEqual(5, t.HordePackSizeMax, "최대 < 최소면 최소로");
            Assert.IsFalse(t.HordeKeepRewardTotal);
            Assert.IsFalse(t.HordeSplitReward);
            Assert.IsFalse(t.HordeMultiplies);
        }

        [Test]
        public void Horde_Json__CurveInterpolatesClampsAndSkipsUnsortedPoints()
        {
            var t = new WaveTuning();
            WaveTuning.ReadHorde(Newtonsoft.Json.Linq.JObject.Parse(
                "{\"enabled\":true,\"normal_count_multiplier\":3,\"normal_count_multiplier_by_wave\":[[1,1.5],[11,2.5],[5,9],[21,0.2]]," +
                "\"role_multiplier_cap\":{\"evasive_charger\":1.2},\"reward_mode\":\"split\"}"), t);
            Assert.AreEqual(3, t.HordeMultiplierByWave.Count, "오름차순이 아닌 점(5)은 버린다");
            Assert.AreEqual(1.5f, t.HordeMultiplier(0, "swarm"), 1e-4f);
            Assert.AreEqual(2.0f, t.HordeMultiplier(6, "swarm"), 1e-4f, "1.5 → 2.5 사이 직선");
            Assert.AreEqual(1.0f, t.HordeMultiplier(21, "swarm"), 1e-4f, "1 아래는 1");
            Assert.AreEqual(1.2f, t.HordeMultiplier(11, "evasive_charger"), 1e-4f);
            Assert.IsTrue(t.HordeSplitReward);
            Assert.IsFalse(t.HordePacks, "계단도 최소·최대도 없으면 무리 없음");
            Assert.IsFalse(t.HordeKeepRewardTotal);
        }

        [Test]
        public void Horde_Json__PackSizeStepsAreStairsClampedAndSorted()
        {
            var t = new WaveTuning();
            WaveTuning.ReadHorde(Newtonsoft.Json.Linq.JObject.Parse(
                "{\"enabled\":true,\"pack_size_min\":6,\"pack_size_max\":12," +
                "\"pack_size_by_wave\":[[2,3,5],[6,0,2],[4,9,4],[3,1],[10,7,7]]}"), t);
            Assert.AreEqual(3, t.HordePackSizeByWave.Count, "값 셋 미만·오름차순 아닌 점(4)은 버린다");
            int lo, hi;
            t.PackSizeRange(1, out lo, out hi);
            Assert.AreEqual(3, lo, "첫 점 앞은 첫 점"); Assert.AreEqual(5, hi);
            t.PackSizeRange(5, out lo, out hi);
            Assert.AreEqual(3, lo, "계단: 다음 점(6) 앞까지 그대로"); Assert.AreEqual(5, hi);
            t.PackSizeRange(6, out lo, out hi);
            Assert.AreEqual(1, lo, "최소는 1 아래로 안 간다"); Assert.AreEqual(2, hi);
            t.PackSizeRange(99, out lo, out hi);
            Assert.AreEqual(7, lo); Assert.AreEqual(7, hi);
            Assert.IsTrue(t.HordePacks);

            var none = new WaveTuning();
            WaveTuning.ReadHorde(Newtonsoft.Json.Linq.JObject.Parse("{\"enabled\":true,\"pack_size_min\":6,\"pack_size_max\":12}"), none);
            none.PackSizeRange(1, out lo, out hi);
            Assert.AreEqual(6, lo, "계단이 없으면 pack_size_min·max"); Assert.AreEqual(12, hi);
        }

        // ------------------------------------------------------------ 잡몹 수 배율

        [TestCase(1UL, 1)]
        [TestCase(2UL, 3)]
        [TestCase(3UL, 9)]
        [TestCase(4UL, 17)]
        [TestCase(5UL, 45)]
        public void Horde_Multiplier__NormalsTimesMultiplierOthersUnchanged(ulong seed, int wave)
        {
            var spec = WaveRig.Build(seed, wave);
            var horde = WaveRig.Build(seed, wave);
            int added = WaveHorde.Expand(Db, Horde, horde);

            int normals = CountNormal(spec);
            Assert.Greater(normals, 0, "이 웨이브에 잡몹이 있어야 시험이 된다");
            // 같은 배율(웨이브 곡선 × 역할 상한)을 쓰는 잡몹끼리 floor(N × m)
            var perMul = new SortedDictionary<int, int>();
            for (int i = 0; i < spec.Entries.Count; i++)
            {
                var e = spec.Entries[i];
                if (!IsNormal(spec, e)) continue;
                int m = Mathf.RoundToInt(Horde.HordeMultiplier(wave, Db.Enemy(e.Species).Role) * 1000);
                int n; perMul.TryGetValue(m, out n); perMul[m] = n + 1;
            }
            int expectTotalNormals = 0;
            foreach (var kv in perMul) expectTotalNormals += (int)((long)kv.Value * kv.Key / 1000);
            Assert.AreEqual(expectTotalNormals - normals, added);
            Assert.AreEqual(spec.Entries.Count + added, horde.Entries.Count);
            Assert.AreEqual(added, CountExtras(horde));

            // 정예·특수(종 상한·역할 밖)·예산 밖 엘리트는 수가 같다. 종마다 셈
            var specCount = new Dictionary<string, int>();
            var hordeCount = new Dictionary<string, int>();
            for (int i = 0; i < spec.Entries.Count; i++)
            {
                var e = spec.Entries[i];
                if (IsNormal(spec, e)) continue;
                string key = e.Species + (e.Elite ? "#elite" : "") + (e.OutsideBudget ? "#out" : "");
                int n; specCount.TryGetValue(key, out n); specCount[key] = n + 1;
            }
            for (int i = 0; i < horde.Entries.Count; i++)
            {
                var e = horde.Entries[i];
                if (IsNormal(horde, e) || WaveHorde.IsExtra(e)) continue;
                string key = e.Species + (e.Elite ? "#elite" : "") + (e.OutsideBudget ? "#out" : "");
                int n; hordeCount.TryGetValue(key, out n); hordeCount[key] = n + 1;
            }
            CollectionAssert.AreEquivalent(specCount, hordeCount);

            // 늘린 엔트리는 잡몹 종이고 원본 바로 뒤, 같은 묶음
            for (int i = 0; i < horde.Entries.Count; i++)
            {
                var e = horde.Entries[i];
                if (!WaveHorde.IsExtra(e)) continue;
                Assert.IsTrue(WaveHorde.IsNormalSpecies(Db, Horde, Db.Enemy(e.Species), horde.Whims), e.Species);
                Assert.Greater(i, 0);
                int j = i - 1;
                while (WaveHorde.IsExtra(horde.Entries[j])) j--;
                Assert.AreEqual(horde.Entries[j].Species, e.Species);
                Assert.AreEqual(horde.Entries[j].Bundle, e.Bundle);
            }
            // 구성의 예산·묶음 수·미리보기는 명세 그대로
            Assert.AreEqual(spec.Budget, horde.Budget);
            Assert.AreEqual(spec.BundleCount, horde.BundleCount);
            CollectionAssert.AreEqual(spec.PreviewSpecies, horde.PreviewSpecies);
            Assert.AreEqual(WaveRig.Cost(spec, false), WaveRig.Cost(horde, false), "예산 안 비용 합은 명세 그대로(늘린 것은 예산 밖)");
        }

        [Test]
        public void Horde_Multiplier__ElitesAndCappedSpeciesNeverMultiply()
        {
            // 웨이브 25 + 우두머리 행차: 엘리트 4(예산 안 2, 밖 2)
            var horde = WaveRig.Build(7, 25, WaveRig.Whims(Ids.Whim.ChiefParade));
            WaveHorde.Expand(Db, Horde, horde);
            int elites = 0;
            for (int i = 0; i < horde.Entries.Count; i++)
            {
                var e = horde.Entries[i];
                if (e.Elite) elites++;
                if (!WaveHorde.IsExtra(e)) continue;
                Assert.IsNull(Db.Enemy(e.Species).SpeciesCap, "종 상한이 있는 종은 늘지 않는다: " + e.Species);
                Assert.AreNotEqual(Ids.Enemy.ShamanCap, e.Species, "역할 밖(ranged_cover)은 늘지 않는다");
            }
            Assert.AreEqual(4, elites);
            // 종 상한(긴 밤 포함)은 늘린 뒤에도 지켜진다
            var caps = new Dictionary<string, int>();
            for (int i = 0; i < horde.Entries.Count; i++)
            {
                int n; caps.TryGetValue(horde.Entries[i].Species, out n); caps[horde.Entries[i].Species] = n + 1;
            }
            foreach (var kv in caps)
            {
                int cap = WavePlanner.SpeciesCap(Db, kv.Key, horde.Whims);
                if (cap > 0) Assert.LessOrEqual(kv.Value, cap, kv.Key);
            }
        }

        [Test]
        public void Horde_Multiplier__BossWaveAndSpecTuningUntouched()
        {
            var rig = WaveRig.Create(3, horde: true);
            var boss = rig.Director.BuildPlan(rig.World, 10);
            Assert.IsNotNull(boss.BossId);
            Assert.AreEqual(0, boss.Entries.Count, "보스 웨이브에는 일반 적이 없다");

            var plan = WaveRig.Build(3, 5);
            int before = plan.Entries.Count;
            Assert.AreEqual(0, WaveHorde.Expand(Db, WaveTuning.From(Db, false), plan));
            Assert.AreEqual(before, plan.Entries.Count);
        }

        [Test]
        public void Horde_Determinism__BuildPlanTwiceIsIdenticalAndExpandsPlanner()
        {
            var rig = WaveRig.Create(11, horde: true);
            for (int wave = 1; wave <= 25; wave++)
            {
                if (DayCycle.IsBossWave(wave)) continue;
                var a = rig.Director.BuildPlan(rig.World, wave);
                var b = rig.Director.BuildPlan(rig.World, wave);
                var c = WaveRig.Build(11, wave);
                WaveHorde.Expand(Db, Horde, c);
                Assert.AreEqual(a.Entries.Count, b.Entries.Count, "웨이브 " + wave);
                Assert.AreEqual(a.Entries.Count, c.Entries.Count, "웨이브 " + wave);
                for (int i = 0; i < a.Entries.Count; i++)
                {
                    Assert.AreEqual(a.Entries[i].Species, b.Entries[i].Species);
                    Assert.AreEqual(a.Entries[i].Bundle, b.Entries[i].Bundle);
                    Assert.AreEqual(a.Entries[i].OutsideBudget, b.Entries[i].OutsideBudget);
                    Assert.AreEqual(a.Entries[i].Species, c.Entries[i].Species);
                    Assert.AreEqual(a.Entries[i].Bundle, c.Entries[i].Bundle);
                }
            }
        }

        // ------------------------------------------------------------ 동시 생존 상한

        [Test]
        public void Horde_MaxAlive__CapIsSpecTimesMultiplier()
        {
            int cap = Db.Waves.MaxAlive * 2;
            var rig = WaveRig.Create(1, horde: true);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(30, cap + 10));
            rig.Steps(1);
            Assert.AreEqual(cap, rig.Enemies.Specs.Count);
            Assert.AreEqual(10, rig.World.Wave.SpawnQueue.Count);
            rig.World.Enemies[0].Dead = true;
            rig.Steps(1);
            Assert.AreEqual(cap + 1, rig.Enemies.Specs.Count, "자리가 나면 한 마리씩");

            // 손잡이를 끈 디렉터는 명세 상한 그대로
            var spec = WaveRig.Create(1);
            spec.Director.BeginWave(spec.World, WaveRig.SlimePlan(30, cap + 10));
            spec.Steps(1);
            Assert.AreEqual(Db.Waves.MaxAlive, spec.Enemies.Specs.Count);
        }

        // ------------------------------------------------------------ 무리 출현

        [Test]
        public void Horde_Pack__BundleOfMinPackSizeComesOutClusteredFromOneEdge()
        {
            var t = Horde;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var rig = WaveRig.Create(seed, horde: true);
                int lo, hi;
                t.PackSizeRange(3, out lo, out hi);
                rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, lo));
                rig.Steps(1);
                var specs = rig.Enemies.Specs;
                Assert.AreEqual(lo, specs.Count);
                // 무리 하나 = 한 자리에서 반경 안: 둘씩 거리 ≤ 지름
                for (int i = 0; i < specs.Count; i++)
                {
                    var p = specs[i].Pos;
                    Assert.That(p.x, Is.InRange(0f, rig.World.ArenaWidth));
                    Assert.That(p.y, Is.InRange(0f, rig.World.ArenaHeight));
                    for (int k = i + 1; k < specs.Count; k++)
                        Assert.LessOrEqual((specs[k].Pos - p).magnitude, t.HordePackScatterRadius * 2f + 0.001f, "씨앗 " + seed);
                }
                // 플레이어(왼쪽 위 (6, 4))에게서 먼 쪽(아래 또는 오른쪽 변 근처)에서 나온다
                Vector2 c = Vector2.zero;
                for (int i = 0; i < specs.Count; i++) c += specs[i].Pos;
                c /= specs.Count;
                float toBottom = rig.World.ArenaHeight - c.y, toRight = rig.World.ArenaWidth - c.x;
                Assert.LessOrEqual(Mathf.Min(toBottom, toRight), t.HordePackScatterRadius + 1f, "씨앗 " + seed);
            }
        }

        [Test]
        public void Horde_Pack__LargeBundleSplitsIntoPacksWithinSizeRange()
        {
            var t = Horde;
            var rig = WaveRig.Create(21, horde: true);
            const int count = 60;
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(30, count));
            rig.Steps(1);
            var specs = rig.Enemies.Specs;
            Assert.AreEqual(count, specs.Count);
            // 무리 수는 ceil(60/12) 이상, ceil(60/6) 이하 → 서로 멀리 떨어진 자리가 여럿
            int far = 0;
            for (int i = 1; i < specs.Count; i++)
                if ((specs[i].Pos - specs[0].Pos).magnitude > t.HordePackScatterRadius * 2f + 0.001f) far++;
            Assert.Greater(far, 0, "무리가 하나뿐이면 안 된다");
        }

        [Test]
        public void Horde_Pack__ElitesAndNonNormalSpeciesSpawnAlone()
        {
            var rig = WaveRig.Create(5, horde: true);
            var plan = new WavePlan { Wave = 30, Tod = WavePlanner.TodOf(Db, 30), Frame = Ids.Frame.Mixed, BundleCount = 1 };
            plan.Entries.Add(new WaveEntry { Species = Ids.Enemy.GolemMenhir, Elite = true, Bundle = 0 });
            plan.Entries.Add(new WaveEntry { Species = Ids.Enemy.ShamanCap, Bundle = 0 });
            var spawnRig = WaveRig.Create(5);
            spawnRig.Director.BeginWave(spawnRig.World, plan);
            rig.Director.BeginWave(rig.World, plan);
            rig.Steps(1);
            spawnRig.Steps(1);
            // 무리 대상이 없으면 난수 소비와 자리가 손잡이를 끈 것과 같다
            Assert.AreEqual(spawnRig.Enemies.Specs.Count, rig.Enemies.Specs.Count);
            for (int i = 0; i < rig.Enemies.Specs.Count; i++)
                Assert.AreEqual(spawnRig.Enemies.Specs[i].Pos, rig.Enemies.Specs[i].Pos);
        }

        // ------------------------------------------------------------ 보상

        [Test]
        public void Horde_Reward__KeepTotalExtrasHaveNoRewardOriginalsDo()
        {
            var rig = WaveRig.Create(9, horde: true);
            var plan = rig.Director.BuildPlan(rig.World, HordeWave);   // 여기서 콘텐츠가 붙어 Tuning이 읽힌다
            rig.Director.Tuning.HordeKeepRewardTotal = true;   // 재미 조정 전의 기본(keep_total)
            rig.Director.Tuning.HordeSplitReward = false;
            var spec = WaveRig.Build(9, HordeWave);
            rig.Director.BeginWave(rig.World, plan);
            for (int i = 0; i < 4000 && !rig.World.Wave.AllReleased; i++) rig.Steps(1);
            rig.Steps(1);
            Assert.AreEqual(plan.Entries.Count, rig.Enemies.Specs.Count);
            int rewarded = 0, unrewarded = 0;
            for (int i = 0; i < rig.Enemies.Specs.Count; i++)
                if (rig.Enemies.Specs[i].NoReward) unrewarded++; else rewarded++;
            Assert.AreEqual(spec.Entries.Count, rewarded, "보상 받는 개체 수 = 명세 구성의 수 → 골드·점수·드랍 추첨 합계 유지");
            Assert.AreEqual(plan.Entries.Count - spec.Entries.Count, unrewarded);
            Assert.Greater(unrewarded, 0);
        }

        [Test]
        public void Horde_Reward__SplitModeEveryoneSpawnsRewarded_RunSplitsTheShare()
        {
            // split(기본): 늘린 적도 NoReward가 아니다 — 한 마리 몫을 나누는 것은 Run(RunFlow.HordeReward, 통합 테스트)이 한다.
            var rig = WaveRig.Create(9, horde: true);
            var plan = rig.Director.BuildPlan(rig.World, HordeWave);
            Assert.IsTrue(rig.Director.Tuning.HordeSplitReward);
            rig.Director.BeginWave(rig.World, plan);
            for (int i = 0; i < 4000 && !rig.World.Wave.AllReleased; i++) rig.Steps(1);
            rig.Steps(1);
            Assert.AreEqual(plan.Entries.Count, rig.Enemies.Specs.Count);
            for (int i = 0; i < rig.Enemies.Specs.Count; i++) Assert.IsFalse(rig.Enemies.Specs[i].NoReward);
            Assert.Greater(CountExtras(plan), 0);
        }

        [Test]
        public void Horde_Reward__FullModeEveryoneRewarded()
        {
            var rig = WaveRig.Create(9, horde: true);
            var plan = rig.Director.BuildPlan(rig.World, HordeWave);
            rig.Director.Tuning.HordeKeepRewardTotal = false;
            rig.Director.BeginWave(rig.World, plan);
            for (int i = 0; i < 4000 && !rig.World.Wave.AllReleased; i++) rig.Steps(1);
            rig.Steps(1);
            Assert.AreEqual(plan.Entries.Count, rig.Enemies.Specs.Count);
            for (int i = 0; i < rig.Enemies.Specs.Count; i++) Assert.IsFalse(rig.Enemies.Specs[i].NoReward);
        }

        [Test]
        public void Horde_Progress__UnrewardedExtrasStillCountInRemainingRatio()
        {
            var rig = WaveRig.Create(9, horde: true);
            var plan = rig.Director.BuildPlan(rig.World, HordeWave);
            rig.Director.Tuning.HordeKeepRewardTotal = true;   // 보상 없는 늘린 개체가 있는 모드
            rig.Director.Tuning.HordeSplitReward = false;
            rig.Director.BeginWave(rig.World, plan);
            rig.Steps(1);
            Assert.Greater(rig.World.Enemies.Count, 0);
            // (시험용 가짜 적 모듈은 웨이브 체력 배율 없이 개체를 만든다 — 웨이브 3에서는 처음 값이 1보다 조금 작다. 줄어드는지만 본다.)
            float before = rig.World.Wave.RemainingRatio;
            Assert.Greater(before, 0.5f);
            // 늘린 개체(보상 없음) 하나를 죽이면 바가 준다
            bool killed = false;
            for (int i = 0; i < rig.World.Enemies.Count; i++)
                if (rig.World.Enemies[i].NoReward) { rig.World.Enemies[i].Dead = true; killed = true; break; }
            Assert.IsTrue(killed, "첫 묶음에 늘린 개체가 있어야 시험이 된다");
            rig.Steps(1);
            Assert.Less(rig.World.Wave.RemainingRatio, before);
        }

        // ------------------------------------------------------------ 결정론

        [Test]
        public void Horde_Determinism__SameSeedTwoRunsSameSpawns()
        {
            List<EnemySpawnSpec> Run()
            {
                var rig = WaveRig.Create(4242, horde: true);
                rig.Director.BeginWave(rig.World, rig.Director.BuildPlan(rig.World, 9));
                for (int i = 0; i < 600; i++)
                {
                    rig.Steps(1);
                    // 몇 마리씩 죽여 상한·묶음 방출이 돌게 한다(틱 번호로만 정한다)
                    if (i % 30 == 0)
                        for (int k = 0; k < rig.World.Enemies.Count; k += 3) rig.World.Enemies[k].Dead = true;
                }
                return rig.Enemies.Specs;
            }
            var a = Run();
            var b = Run();
            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].DefId, b[i].DefId, "#" + i);
                Assert.AreEqual(a[i].Pos, b[i].Pos, "#" + i);
                Assert.AreEqual(a[i].NoReward, b[i].NoReward, "#" + i);
                Assert.AreEqual(a[i].Elite, b[i].Elite, "#" + i);
            }
        }
    }
}
