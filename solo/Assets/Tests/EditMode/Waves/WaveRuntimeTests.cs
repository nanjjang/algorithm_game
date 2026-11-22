using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>웨이브 런타임: 시작, 묶음 방출, 동시 상한, 지체 방지, 변덕, 등장 자리. 06 요약서 1.4·2.2·3.5절, 00 문서 D-64.</summary>
    public class WaveRuntimeTests
    {
        static IContentDb Db => TestContent.Db;

        static int CountEvents(SimWorld w, SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < w.Events.Count; i++) if (w.Events[i].Type == type) n++;
            return n;
        }

        static bool TryFindEvent(SimWorld w, SimEventType type, out SimEvent found)
        {
            for (int i = 0; i < w.Events.Count; i++)
                if (w.Events[i].Type == type) { found = w.Events[i]; return true; }
            found = default(SimEvent);
            return false;
        }

        // ------------------------------------------------------------ BeginWave 계약

        [Test]
        public void S07_Contract__BeginWaveFillsStatsFromPlan()
        {
            var rig = WaveRig.Create(5);
            var plan = WaveRig.Build(5, 45, WaveRig.Whims(Ids.Whim.Crowding, Ids.Whim.ThickHide));
            int prelude = rig.Director.BeginWave(rig.World, plan);

            var ws = rig.World.Wave;
            Assert.AreEqual(0, prelude);
            Assert.AreSame(plan, ws.Plan);
            Assert.AreEqual(45, ws.Wave);
            Assert.AreEqual(45, ws.Stats.Wave);
            Assert.IsFalse(ws.Stats.IsBoss);
            Assert.IsFalse(ws.Stats.PhantomFirst);
            CollectionAssert.AreEqual(plan.Whims, ws.Stats.Whims);
            Assert.AreEqual(0, ws.WaveTick);
            Assert.IsFalse(ws.Stalled);
            Assert.IsFalse(ws.AllReleased);
            Assert.AreEqual(1f, ws.RemainingRatio, 0.0001f);

            SimEvent started;
            Assert.IsTrue(TryFindEvent(rig.World, SimEventType.WaveStarted, out started));
            Assert.AreEqual(45, started.A);
            Assert.AreEqual(plan.Frame, started.Id);
            Assert.AreEqual(-1, started.Slot);
        }

        [Test]
        public void S07_Contract__BeginWaveBossAndPhantomFirstStats()
        {
            var rig = WaveRig.Create(5);
            var plan = WaveRig.Build(5, 10, null, null, true);
            Assert.AreEqual(0, rig.Director.BeginWave(rig.World, plan));
            var ws = rig.World.Wave;
            Assert.AreEqual(10, ws.Stats.Wave);
            Assert.IsTrue(ws.Stats.IsBoss);
            Assert.IsTrue(ws.Stats.PhantomFirst);
            Assert.AreEqual(0, ws.Stats.Whims.Count);
            Assert.AreEqual(Ids.Boss.MirrorPhantom, plan.BossId);
        }

        // 05 요약서 2.1절: BeginWave가 그 웨이브의 상한을 적는다(0 이하는 '상한 없음'이라 쓰지 않는다)
        [TestCase(1, 2)]
        [TestCase(5, 3)]
        [TestCase(9, 4)]
        [TestCase(13, 5)]
        [TestCase(29, 5)]
        [TestCase(30, 6)]
        [TestCase(64, 6)]
        public void S05_TelegraphCap__BeginWaveSetsCap(int wave, int cap)
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.Build(1, wave));
            Assert.AreEqual(cap, rig.World.Wave.TelegraphCap);
            Assert.Greater(rig.World.Wave.TelegraphCap, 0);
        }

        // 1.4절 변덕 값
        [Test]
        public void S07_Whims__BeginWaveAppliesWhimValues()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.Build(1, 22, WaveRig.Whims()));
            var plain = rig.World.Wave;
            Assert.AreEqual(420, plain.BundleIntervalTicks);
            Assert.AreEqual(1f, plain.EnemyHpMul, 0.0001f);
            Assert.AreEqual(1f, plain.EnemyShotCountMul, 0.0001f);
            Assert.AreEqual(1f, plain.EnemyShotSpeedMul, 0.0001f);
            Assert.IsFalse(plain.LastBreath);

            rig.Director.BeginWave(rig.World, WaveRig.Build(1, 23, WaveRig.Whims(Ids.Whim.ThickHide, Ids.Whim.SlowBarrage, Ids.Whim.LastBreath)));
            var ws = rig.World.Wave;
            Assert.AreEqual(1.3f, ws.EnemyHpMul, 0.0001f);
            Assert.AreEqual(1.5f, ws.EnemyShotCountMul, 0.0001f);
            Assert.AreEqual(0.8f, ws.EnemyShotSpeedMul, 0.0001f);
            Assert.IsTrue(ws.LastBreath);
            Assert.AreEqual(420, ws.BundleIntervalTicks);
        }

        [Test]
        public void S07_TV_071__CrowdingBundleIntervalIs240Ticks()
        {
            var rig = WaveRig.Create();
            var plan = WaveRig.SlimePlan(22, 4, 4);
            plan.Whims.Add(Ids.Whim.Crowding);
            rig.Director.BeginWave(rig.World, plan);
            Assert.AreEqual(240, rig.World.Wave.BundleIntervalTicks);

            rig.Steps(1);                       // 틱 0: 첫 묶음
            Assert.AreEqual(4, rig.Enemies.Specs.Count);
            rig.Steps(239);                     // 틱 1~239
            Assert.AreEqual(4, rig.Enemies.Specs.Count);
            rig.Steps(1);                       // 틱 240
            Assert.AreEqual(8, rig.Enemies.Specs.Count);
        }

        // ------------------------------------------------------------ 3.5절 묶음 방출

        [Test]
        public void S07_Runtime__FirstBundleAtWaveTickZero()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 5, 5));
            Assert.AreEqual(0, rig.Enemies.Specs.Count);
            rig.Steps(1);
            Assert.AreEqual(5, rig.Enemies.Specs.Count);
            Assert.AreEqual(1, rig.World.Wave.NextBundle);
            Assert.AreEqual(1, rig.World.Wave.WaveTick);
            for (int i = 0; i < rig.Enemies.Specs.Count; i++)
            {
                Assert.AreEqual(Ids.Enemy.SlimeMoss, rig.Enemies.Specs[i].DefId);
                Assert.IsFalse(rig.Enemies.Specs[i].Elite);
                Assert.IsFalse(rig.Enemies.Specs[i].NoReward);
                Assert.AreEqual(0, rig.Enemies.Specs[i].SummonerId);
            }
        }

        [Test]
        public void S07_TV_068__AtAliveCapQueuedEnemyAppearsWhenOneDies()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(30, 50));
            rig.Steps(1);
            Assert.AreEqual(40, rig.Enemies.Specs.Count);
            Assert.AreEqual(40, rig.World.AliveEnemies());
            Assert.AreEqual(10, rig.World.Wave.SpawnQueue.Count);
            rig.Steps(5);
            Assert.AreEqual(40, rig.Enemies.Specs.Count, "자리가 없으면 기다린다");

            rig.World.Enemies[0].Dead = true;
            rig.Steps(1);
            Assert.AreEqual(41, rig.Enemies.Specs.Count, "한 마리 죽으면 대기 중인 한 마리가 나온다");
            Assert.AreEqual(40, rig.World.AliveEnemies());
            Assert.AreEqual(9, rig.World.Wave.SpawnQueue.Count);
            Assert.IsFalse(rig.Director.IsCleared(rig.World));
        }

        [Test]
        public void S07_TV_069__NextBundleImmediatelyWhenThreeOrFewerAlive()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 5, 6));
            rig.Steps(1);
            Assert.AreEqual(5, rig.Enemies.Specs.Count);
            rig.Steps(300);                     // 방출 뒤 300틱, 살아 있는 적 5
            Assert.AreEqual(5, rig.Enemies.Specs.Count);

            rig.World.Enemies[0].Dead = true;
            rig.World.Enemies[1].Dead = true;   // 살아 있는 적 3
            rig.Steps(1);
            Assert.AreEqual(11, rig.Enemies.Specs.Count, "다음 틱에 다음 묶음이 바로 나온다");
        }

        [Test]
        public void S07_TV_070__FourAliveReleasesAt420TicksNotAt419()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 4, 6));
            rig.Steps(1);                       // 틱 0에 방출
            Assert.AreEqual(4, rig.Enemies.Specs.Count);
            rig.Steps(419);                     // 틱 1~419: 방출 뒤 419틱까지
            Assert.AreEqual(4, rig.Enemies.Specs.Count, "419틱에는 방출하지 않는다");
            rig.Steps(1);                       // 틱 420
            Assert.AreEqual(10, rig.Enemies.Specs.Count, "420틱에 방출한다");
            Assert.IsTrue(rig.World.Wave.AllReleased);
        }

        [Test]
        public void S07_TV_072__StallAt7200ReleasesAllRemainingBundles()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(14, 4, 20, 30));
            rig.Steps(1);
            Assert.AreEqual(4, rig.Enemies.Specs.Count);
            Assert.IsFalse(rig.World.Wave.Stalled);

            // 웨이브 시계만 7,199로 당긴다(남은 묶음 2)
            rig.World.Wave.WaveTick = 7199;
            rig.World.Events.Clear();
            rig.Steps(1);                       // 이 틱 끝에 시계가 7,200
            var ws = rig.World.Wave;
            Assert.AreEqual(7200, ws.WaveTick);
            Assert.IsTrue(ws.Stalled);
            Assert.IsTrue(ws.AllReleased);
            Assert.AreEqual(50, ws.SpawnQueue.Count);
            SimEvent stalled;
            Assert.IsTrue(TryFindEvent(rig.World, SimEventType.WaveStalled, out stalled));
            Assert.AreEqual(14, stalled.A);

            rig.Steps(1);                       // 웨이브 시계 7,200인 틱: 40 상한 안에서 전부
            Assert.AreEqual(40, rig.World.AliveEnemies());
            Assert.AreEqual(40, rig.Enemies.Specs.Count);
            Assert.AreEqual(14, ws.SpawnQueue.Count);
            rig.Steps(600);
            Assert.AreEqual(40, rig.Enemies.Specs.Count, "간격으로는 더 나오지 않는다(자리가 나야 나온다)");
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.WaveStalled));

            for (int i = 0; i < 3; i++) rig.World.Enemies[i].Dead = true;
            rig.Steps(1);
            Assert.AreEqual(43, rig.Enemies.Specs.Count);
        }

        [Test]
        public void S07_TV_072__NoStallBefore7200AndNoneOnBossWave()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(14, 4, 20));
            rig.Steps(1);
            rig.World.Wave.WaveTick = 7198;
            rig.Steps(1);
            Assert.AreEqual(7199, rig.World.Wave.WaveTick);
            Assert.IsFalse(rig.World.Wave.Stalled);

            var boss = WaveRig.Create();
            boss.Director.BeginWave(boss.World, WaveRig.Build(1, 20));
            boss.World.Wave.WaveTick = 7199;
            boss.Steps(3);
            Assert.AreEqual(7202, boss.World.Wave.WaveTick);
            Assert.IsFalse(boss.World.Wave.Stalled);
            Assert.AreEqual(0, CountEvents(boss.World, SimEventType.WaveStalled));
            Assert.AreEqual(0, boss.Enemies.Specs.Count);
        }

        // 점수용 시계는 WaveCombat 동안만 흐르고, 그 밖에서는 방출도 없다
        [Test]
        public void S07_Runtime__WaveTickAndSpawnsOnlyDuringWaveCombat()
        {
            var rig = WaveRig.Create();
            rig.World.Run.Phase = RunPhase.WavePrelude;
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 5));
            rig.Steps(30);
            Assert.AreEqual(0, rig.World.Wave.WaveTick);
            Assert.AreEqual(0, rig.Enemies.Specs.Count);

            rig.World.Run.Phase = RunPhase.WaveCombat;
            rig.Steps(10);
            Assert.AreEqual(10, rig.World.Wave.WaveTick);
            Assert.AreEqual(5, rig.Enemies.Specs.Count);

            rig.World.Run.Phase = RunPhase.Camp;
            rig.Steps(10);
            Assert.AreEqual(10, rig.World.Wave.WaveTick);
        }

        [Test]
        public void S07_Runtime__NoPlayerDefersRelease()
        {
            var rig = WaveRig.Create();
            rig.World.Players[0].Downed = true;         // NearestPlayer == null
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 5));
            rig.Steps(20);
            Assert.AreEqual(0, rig.Enemies.Specs.Count);
            Assert.AreEqual(0, rig.World.Wave.NextBundle);
            Assert.IsFalse(rig.Director.IsCleared(rig.World));

            rig.World.Players[0].Downed = false;
            rig.Steps(1);
            Assert.AreEqual(5, rig.Enemies.Specs.Count);
        }

        [Test]
        public void S07_Runtime__ClearedWhenAllReleasedAndNoneAliveIncludingSummons()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 2, 2));
            Assert.IsFalse(rig.Director.IsCleared(rig.World));
            rig.Steps(1);                       // 묶음 0
            Assert.IsFalse(rig.Director.IsCleared(rig.World));
            rig.Steps(1);                       // 살아 있는 적 2 ≤ 3 → 묶음 1
            Assert.AreEqual(4, rig.Enemies.Specs.Count);
            Assert.IsTrue(rig.World.Wave.AllReleased);
            Assert.IsFalse(rig.Director.IsCleared(rig.World));

            // 갈라진·불러낸 개체(보상 없음)도 살아 있으면 아직 아니다
            for (int i = 0; i < rig.World.Enemies.Count; i++) rig.World.Enemies[i].Dead = true;
            rig.World.Enemies.Add(new EnemyState { Id = 9999, DefId = Ids.Enemy.SlimeMoss, NoReward = true, SummonerId = 3 });
            Assert.IsFalse(rig.Director.IsCleared(rig.World));
            rig.World.Enemies[rig.World.Enemies.Count - 1].PendingDeath = true;
            Assert.IsTrue(rig.Director.IsCleared(rig.World));
        }

        [Test]
        public void S07_Runtime__BossWaveNeverClearsByEnemyCount()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.Build(1, 10));
            rig.Steps(5);
            Assert.IsFalse(rig.Director.IsCleared(rig.World));
            Assert.AreEqual(0, rig.Enemies.Specs.Count);
        }

        [Test]
        public void S07_Runtime__EliteEntrySpawnsAsElite()
        {
            var rig = WaveRig.Create();
            var plan = WaveRig.SlimePlan(12, 1);
            plan.Entries.Add(new WaveEntry { Species = Ids.Enemy.GolemMenhir, Elite = true, Bundle = 0 });
            rig.Director.BeginWave(rig.World, plan);
            rig.Steps(1);
            Assert.AreEqual(2, rig.Enemies.Specs.Count);
            Assert.AreEqual(Ids.Enemy.GolemMenhir, rig.Enemies.Specs[1].DefId);
            Assert.IsTrue(rig.Enemies.Specs[1].Elite);
        }

        [Test]
        public void S07_Runtime__RemainingRatioFallsAsEnemiesLoseHp()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(1, 2, 2));
            rig.Steps(1);
            Assert.AreEqual(1f, rig.World.Wave.RemainingRatio, 0.001f);
            // 웨이브 1: 체력 배율 1 → 슬라임 20 × 4 = 80. 한 마리가 죽으면 3/4
            rig.World.Enemies[0].Dead = true;
            rig.Steps(1);
            Assert.AreEqual(0.75f, rig.World.Wave.RemainingRatio, 0.001f);
            for (int i = 0; i < rig.World.Enemies.Count; i++) rig.World.Enemies[i].Dead = true;
            rig.Steps(1);
            Assert.AreEqual(0f, rig.World.Wave.RemainingRatio, 0.001f);
        }

        // ------------------------------------------------------------ 마지막 숨 (D-64)

        [Test]
        public void S07_TV_105__LastBreathRequestsDeathCircleWithStagger12AndNoBypass()
        {
            var rig = WaveRig.Create();
            var plan = WaveRig.SlimePlan(23, 3);
            plan.Whims.Add(Ids.Whim.LastBreath);
            rig.Director.BeginWave(rig.World, plan);
            Assert.IsTrue(rig.World.Wave.LastBreath);
            Assert.AreEqual(0, rig.Threats.Requests.Count);

            var dead = new EnemyState { Id = 77, DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(9.5f, 4.25f), PendingDeath = true };
            rig.Director.OnEnemyKilled(rig.World, dead);

            Assert.AreEqual(1, rig.Threats.Requests.Count);
            var req = rig.Threats.Requests[0];
            Assert.AreEqual(12, req.StaggerIfBlockedTicks);
            Assert.IsFalse(req.Bypass);
            Assert.AreEqual(0, req.OwnerEnemyId, "죽은 적을 주인으로 두지 않는다");
            Assert.AreEqual(1, req.Zones.Count);
            var zone = req.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, zone.Shape);
            Assert.AreEqual(ZoneAnchor.Fixed, zone.Anchor);
            Assert.AreEqual(9.5f, zone.Pos.x, 0.0001f);
            Assert.AreEqual(4.25f, zone.Pos.y, 0.0001f);
            Assert.AreEqual(1.2f, zone.Radius, 0.0001f);
            Assert.AreEqual(48, zone.TelegraphTicks);
            Assert.AreEqual(1, zone.BaseDamage);
            Assert.IsFalse(zone.Harmless);
            Assert.IsNull(zone.LeavesHazard);
        }

        // 갈라진 슬라임·불러낸 개체의 죽음에도 생긴다. 같은 틱 세 마리면 요청 셋(미루기는 Threats 몫)
        [Test]
        public void S07_TV_106__LastBreathOnEveryDeathIncludingSummons()
        {
            var rig = WaveRig.Create();
            var plan = WaveRig.SlimePlan(23, 3);
            plan.Whims.Add(Ids.Whim.LastBreath);
            rig.Director.BeginWave(rig.World, plan);
            for (int i = 0; i < 3; i++)
                rig.Director.OnEnemyKilled(rig.World, new EnemyState { Id = 10 + i, Pos = new Vector2(5f + i, 5f), NoReward = i == 2, SummonerId = i == 2 ? 10 : 0 });
            Assert.AreEqual(3, rig.Threats.Requests.Count);
            var casts = new HashSet<int>();
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(12, rig.Threats.Requests[i].StaggerIfBlockedTicks);
                Assert.IsFalse(rig.Threats.Requests[i].Bypass);
                Assert.AreEqual(5f + i, rig.Threats.Requests[i].Zones[0].Pos.x, 0.0001f);
                casts.Add(rig.Threats.Requests[i].CastId);
            }
            Assert.AreEqual(3, casts.Count, "원마다 시전 번호가 다르다");

            // 웨이브가 끝나면 아직 터지지 않은 원은 바로 사라진다
            rig.Director.EndWave(rig.World);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, rig.Threats.Cancelled);
        }

        [Test]
        public void S07_LastBreath__NoCircleWithoutWhimOrOutsideCombat()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(23, 3));
            rig.Director.OnEnemyKilled(rig.World, new EnemyState { Id = 1, Pos = new Vector2(5f, 5f) });
            Assert.AreEqual(0, rig.Threats.Requests.Count);

            var plan = WaveRig.SlimePlan(24, 3);
            plan.Whims.Add(Ids.Whim.LastBreath);
            rig.Director.BeginWave(rig.World, plan);
            rig.World.Run.Phase = RunPhase.RunEnded;
            rig.Director.OnEnemyKilled(rig.World, new EnemyState { Id = 2, Pos = new Vector2(5f, 5f) });
            Assert.AreEqual(0, rig.Threats.Requests.Count);
        }

        // ------------------------------------------------------------ 좁은 들

        static WaveRig NarrowField(out int prelude, bool answerTiming)
        {
            var rig = WaveRig.Create();
            rig.Threats.AnswerTiming = answerTiming;
            rig.World.Run.Phase = RunPhase.WavePrelude;
            var plan = WaveRig.SlimePlan(22, 6);
            plan.Whims.Add(Ids.Whim.NarrowField);
            prelude = rig.Director.BeginWave(rig.World, plan);
            return rig;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void S07_TV_107__NarrowFieldTelegraphs180TicksThenThornBorder(bool answerTiming)
        {
            int prelude;
            var rig = NarrowField(out prelude, answerTiming);
            Assert.AreEqual(180, prelude);

            Assert.AreEqual(1, rig.Threats.Requests.Count);
            var req = rig.Threats.Requests[0];
            Assert.IsTrue(req.Bypass, "상한·R5를 보지 않는다");
            Assert.AreEqual(0, req.StaggerIfBlockedTicks);
            Assert.AreEqual(4, req.Zones.Count);
            float area = 0f;
            for (int i = 0; i < req.Zones.Count; i++)
            {
                var z = req.Zones[i];
                Assert.AreEqual(ZoneShape.Band, z.Shape);
                Assert.AreEqual(ZoneAnchor.Fixed, z.Anchor);
                Assert.AreEqual(180, z.TelegraphTicks);
                Assert.AreEqual(2f, z.Width, 0.0001f);
                Assert.IsFalse(z.Harmless);
                area += z.Width * z.Length;
            }
            // 가장자리 2칸 띠 = 24×14 − 20×10, 겹치지 않는다
            Assert.AreEqual(24f * 14f - 20f * 10f, area, 0.001f);

            // 예고 동안에는 가시밭이 없다
            rig.Steps(180);
            Assert.AreEqual(0, rig.Threats.Hazards.Count);
            Assert.AreEqual(0, rig.Enemies.Specs.Count);
            rig.World.Run.Phase = RunPhase.WaveCombat;
            rig.Steps(1);                       // 틱 180: 예고가 끝나는 틱에 가시밭
            Assert.AreEqual(4, rig.Threats.Hazards.Count);
            float hazardArea = 0f;
            for (int i = 0; i < 4; i++)
            {
                var h = rig.Threats.Hazards[i];
                Assert.AreEqual(180, rig.Threats.HazardTicks[i]);
                Assert.AreEqual(HazardKind.ThornBorder, h.Kind);
                Assert.AreEqual(ZoneShape.Band, h.Shape);
                Assert.AreEqual(2f, h.Width, 0.0001f);
                Assert.LessOrEqual(h.DurationTicks, 0, "웨이브 끝까지");
                Assert.AreEqual(1, h.BaseDamage);
                Assert.AreEqual(90, h.ContactCooldownTicks);
                Assert.AreEqual(0, h.MaxCount, "가시덩굴의 가시밭 한도에 세지 않는다");
                Assert.IsFalse(h.SlowOnly);
                hazardArea += h.Width * h.Length;
            }
            Assert.AreEqual(136f, hazardArea, 0.001f);
            rig.Steps(50);
            Assert.AreEqual(4, rig.Threats.Hazards.Count, "한 번만 깐다");
        }

        [Test]
        public void S07_TV_107__NoThornBorderWithoutNarrowField()
        {
            var rig = WaveRig.Create();
            Assert.AreEqual(0, rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(22, 6)));
            rig.Steps(200);
            Assert.AreEqual(0, rig.Threats.Requests.Count);
            Assert.AreEqual(0, rig.Threats.Hazards.Count);
        }

        // 계약 벡터: 좁은 들에서 스폰 자리가 가시 띠(폭 2칸) 바로 안쪽
        [Test]
        public void S07_Contract__NarrowFieldSpawnsJustInsideThornBorder()
        {
            int prelude;
            var rig = NarrowField(out prelude, true);
            var slime = Db.Enemy(Ids.Enemy.SlimeMoss);
            float r = slime.BodyRadius;
            rig.World.Players[0].Pos = new Vector2(6f, 4f);       // 왼쪽 위 → 먼 변은 아래·오른쪽
            for (int i = 0; i < 300; i++)
            {
                Vector2 p = rig.Director.PickSpawnPoint(rig.World, slime);
                Assert.GreaterOrEqual(p.x, 2f, "x " + p);
                Assert.LessOrEqual(p.x, 22f, "x " + p);
                Assert.GreaterOrEqual(p.y, 2f, "y " + p);
                Assert.LessOrEqual(p.y, 12f, "y " + p);
                bool onBottom = Mathf.Abs(p.y - (12f - r)) < 0.001f;
                bool onRight = Mathf.Abs(p.x - (22f - r)) < 0.001f;
                Assert.IsTrue(onBottom || onRight, "가시 띠 바로 안쪽이어야 한다: " + p);
            }
        }

        // ------------------------------------------------------------ 등장 자리 (05 요약서 4절 끝)

        static void AssertOnSides(WaveRig rig, Vector2 player, bool bottom, bool right)
        {
            var slime = Db.Enemy(Ids.Enemy.SlimeMoss);
            float r = slime.BodyRadius;
            rig.World.Players[0].Pos = player;
            bool sawHorizontal = false, sawVertical = false;
            for (int i = 0; i < 400; i++)
            {
                Vector2 p = rig.Director.PickSpawnPoint(rig.World, slime);
                bool onH = Mathf.Abs(p.y - (bottom ? 14f - r : r)) < 0.001f;
                bool onV = Mathf.Abs(p.x - (right ? 24f - r : r)) < 0.001f;
                Assert.IsTrue(onH || onV, "플레이어 " + player + " 자리 " + p);
                // 모서리 1칸 제외
                if (onH && !onV) { Assert.GreaterOrEqual(p.x, 1f); Assert.LessOrEqual(p.x, 23f); }
                if (onV && !onH) { Assert.GreaterOrEqual(p.y, 1f); Assert.LessOrEqual(p.y, 13f); }
                sawHorizontal |= onH;
                sawVertical |= onV;
            }
            Assert.IsTrue(sawHorizontal && sawVertical, "두 변에서 고르게 나와야 한다");
        }

        [Test]
        public void S05_Spawn__FarTwoSidesWithCornerExclusion()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 1));
            AssertOnSides(rig, new Vector2(4f, 3f), true, true);      // 왼쪽 위 → 아래·오른쪽
            AssertOnSides(rig, new Vector2(20f, 11f), false, false);  // 오른쪽 아래 → 위·왼쪽
            AssertOnSides(rig, new Vector2(20f, 3f), true, false);    // 오른쪽 위 → 아래·왼쪽
            AssertOnSides(rig, new Vector2(4f, 11f), false, true);    // 왼쪽 아래 → 위·오른쪽
        }

        // 정확히 가운데면 위·오른쪽
        [Test]
        public void S05_Spawn__TieAtCenterIsTopAndRight()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 1));
            AssertOnSides(rig, new Vector2(12f, 7f), false, true);
        }

        [Test]
        public void S05_Spawn__AvoidsOtherEnemiesWithinOneCell()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(3, 5));
            rig.Steps(1);
            Assert.AreEqual(5, rig.World.Enemies.Count);
            int close = 0;
            for (int a = 0; a < rig.World.Enemies.Count; a++)
                for (int b = a + 1; b < rig.World.Enemies.Count; b++)
                    if ((rig.World.Enemies[a].Pos - rig.World.Enemies[b].Pos).magnitude < 1f) close++;
            Assert.AreEqual(0, close, "두 변(길이 34칸)에 5마리면 1칸 이내로 겹치지 않는다");
        }

        // 가시덩굴 예외: 벽 2칸 이상 + 플레이어 3칸 이상
        [Test]
        public void S05_Spawn__VineInsideArenaAwayFromWallAndPlayer()
        {
            var rig = WaveRig.Create();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(11, 1));
            var vine = Db.Enemy(Ids.Enemy.VineThorn);
            var player = new Vector2(12f, 7f);
            rig.World.Players[0].Pos = player;
            for (int i = 0; i < 300; i++)
            {
                Vector2 p = rig.Director.PickSpawnPoint(rig.World, vine);
                Assert.GreaterOrEqual(p.x, 2f);
                Assert.LessOrEqual(p.x, 22f);
                Assert.GreaterOrEqual(p.y, 2f);
                Assert.LessOrEqual(p.y, 12f);
                Assert.GreaterOrEqual((p - player).magnitude, 3f, "자리 " + p);
            }
        }

        [Test]
        public void S07_Runtime__SameSeedSameSpawnPositions()
        {
            var a = WaveRig.Create(4242);
            var b = WaveRig.Create(4242);
            a.Director.BeginWave(a.World, WaveRig.Build(4242, 9));
            b.Director.BeginWave(b.World, WaveRig.Build(4242, 9));
            a.Steps(500);
            b.Steps(500);
            Assert.Greater(a.Enemies.Specs.Count, 0);
            Assert.AreEqual(a.Enemies.Specs.Count, b.Enemies.Specs.Count);
            for (int i = 0; i < a.Enemies.Specs.Count; i++)
            {
                Assert.AreEqual(a.Enemies.Specs[i].DefId, b.Enemies.Specs[i].DefId);
                Assert.AreEqual(a.Enemies.Specs[i].Pos, b.Enemies.Specs[i].Pos);
            }
        }

        // 구성 전체가 빠짐없이 나온다(각본 웨이브 4: 술사 1, 고슴도치 1, 늑대 8, 슬라임 2)
        [Test]
        public void S07_Runtime__WholePlanIsReleasedInBundleOrder()
        {
            var rig = WaveRig.Create(8);
            var plan = WaveRig.Build(8, 4);
            rig.Director.BeginWave(rig.World, plan);
            for (int i = 0; i < 4000 && !rig.World.Wave.AllReleased; i++) rig.Steps(1);
            rig.Steps(1);
            Assert.IsTrue(rig.World.Wave.AllReleased);
            Assert.AreEqual(12, rig.Enemies.Specs.Count);
            int wolves = 0, slimes = 0, shamans = 0, hogs = 0;
            for (int i = 0; i < rig.Enemies.Specs.Count; i++)
            {
                string id = rig.Enemies.Specs[i].DefId;
                if (id == Ids.Enemy.WolfBramble) wolves++;
                else if (id == Ids.Enemy.SlimeMoss) slimes++;
                else if (id == Ids.Enemy.ShamanCap) shamans++;
                else if (id == Ids.Enemy.HogNeedle) hogs++;
            }
            Assert.AreEqual(8, wolves);
            Assert.AreEqual(2, slimes);
            Assert.AreEqual(1, shamans);
            Assert.AreEqual(1, hogs);
        }

        // 모듈 등록: 기본 묶음에 디렉터가 들어간다
        [Test]
        public void S00_Factory__DefaultModuleSetUsesWaveDirector()
        {
            Assert.IsInstanceOf<WaveDirector>(SimModuleSet.CreateDefault().Waves);
        }
    }
}
