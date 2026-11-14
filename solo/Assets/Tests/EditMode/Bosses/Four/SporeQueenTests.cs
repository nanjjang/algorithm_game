using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>천 갓(05 요약서 9.3절). Threats·Enemies·Player는 가짜(G07의 BossWorld).</summary>
    public class SporeQueenTests
    {
        const string Id = Ids.Boss.SporeQueen;
        static readonly Vector2 Center = new Vector2(12f, 7f);

        // 9.3절: 1단계는 뿌리(꼬리표 보스 + 뿌리), 중앙 반경 1칸 안에서만 움직인다.
        [Test]
        public void S06_9_3__PhaseOneStaysWithinAnchorRadius()
        {
            var bw = FourKit.Make(Id, 30, FourKit.NoPattern, still: false);
            Assert.That((bw.Body.Pos - Center).magnitude, Is.LessThan(FourKit.Eps), "전장 가운데에서 시작한다");
            Assert.IsTrue(bw.Body.Has(EnemyTags.Boss));
            Assert.IsTrue(bw.Body.Has(EnemyTags.Rooted));
            bw.P.Pos = new Vector2(22f, 7f);
            bw.FinishIntro();
            for (int i = 0; i < 400; i++)
            {
                bw.Step(1);
                Assert.LessOrEqual((bw.Body.Pos - Center).magnitude, 1f + FourKit.Eps);
            }
            // 플레이어 쪽으로 반경 끝까지는 간다(속도 1.0칸/초. 뼈대의 쫓기는 목표 0.1칸 앞에서 멈춘다).
            Assert.That(bw.Body.Pos.x, Is.EqualTo(13f).Within(bw.Ctx.Tuning.ChaseStopDistance + 0.02f));
            Assert.That(bw.Body.Pos.y, Is.EqualTo(7f).Within(0.01f));
        }

        // 9.3절: 2단계(50% 이하)는 뿌리를 뽑아(꼬리표 보스) 반경 밖으로 쫓아간다.
        [Test]
        public void S06_9_3__PhaseTwoUprootsAndChases()
        {
            var bw = FourKit.Make(Id, 30, FourKit.NoPattern, still: false);
            bw.P.Pos = new Vector2(22f, 7f);
            bw.FinishIntro();
            FourKit.ToPhase(bw, 1);
            Assert.AreEqual(1, bw.Boss.Phase);
            Assert.AreEqual(BossFightState.PhaseTransition, bw.Boss.State);
            Assert.IsFalse(bw.Body.Has(EnemyTags.Rooted));
            Assert.IsTrue(bw.Body.Has(EnemyTags.Boss));
            bw.Step(36 + 300);
            Assert.Greater((bw.Body.Pos - Center).magnitude, 3f);
        }

        // 9.3절 spore_ring: 갓 부풂 42틱 → 16방향(22.5도) 고리 3겹(2단계 4겹), 36틱 간격, 겹마다 11.25도 회전, 속도 4, 대 탄 0.18.
        [TestCase(0, 3)]
        [TestCase(1, 4)]
        public void S06_9_3__SporeRingLayers(int phase, int layers)
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternRing);
            bw.FinishIntro();
            if (phase > 0) FourKit.ToPhase(bw, phase);
            int r = FourKit.WaitPattern(bw, SporeQueenBrain.PatternRing);
            Assert.GreaterOrEqual(r, 0);
            Assert.AreEqual(0, bw.Threats.Tickets.Count, "구역 없는 예고(시전 동작)");
            bw.StepTo(r + 42);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            for (int k = 0; k < layers; k++)
            {
                FourKit.RunThrough(bw, r + 42 + 36 * k);
                Assert.AreEqual(16 * (k + 1), bw.Threats.Shots.Count);
                Assert.AreEqual(r + 42 + 36 * k, bw.Threats.ShotTicks[16 * k]);
            }
            var first = bw.Threats.Shots[0];
            Assert.That(first.Speed, Is.EqualTo(4f));
            Assert.That(first.Radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.AreEqual(1, first.BaseDamage);
            Assert.That(Vector2.Angle(first.Dir, bw.P.Pos - bw.Body.Pos), Is.LessThan(0.01f));
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[1].Dir), Is.EqualTo(22.5f).Within(0.01f));
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[16].Dir), Is.EqualTo(11.25f).Within(0.01f));
            // 마지막 겹 뒤 빈틈 90틱.
            int end = r + 42 + 36 * (layers - 1);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(end + 90);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(16 * layers, bw.Threats.Shots.Count);
        }

        // 9.3절 spore_cloud: circle r2.0 × 3(1개 플레이어 위치, 2개는 플레이어와·서로 중심 ≥ 4.0), 60틱, 판정 1 + 포자 안개(상한 1개로 센다).
        [Test]
        public void S06_9_3__SporeCloudZones()
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternCloud);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, SporeQueenBrain.PatternCloud);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var t = bw.Threats.Tickets[0];
            Assert.AreEqual(r, t.RequestTick);
            Assert.IsTrue(t.Request.CountAsOne, "구름 3개는 동시 예고 상한에 1개로 센다");
            Assert.AreEqual(bw.Body.Id, t.Request.OwnerEnemyId);
            var zones = t.Request.Zones;
            Assert.AreEqual(3, zones.Count);
            Assert.AreEqual(bw.P.Pos, zones[0].Pos);
            for (int i = 0; i < zones.Count; i++)
            {
                Assert.AreEqual(ZoneShape.Circle, zones[i].Shape);
                Assert.That(zones[i].Radius, Is.EqualTo(2f));
                Assert.AreEqual(60, zones[i].TelegraphTicks);
                Assert.AreEqual(1, zones[i].BaseDamage);
                Assert.AreEqual(PlayerDamageSource.Telegraph, zones[i].DamageSource);
                Assert.IsNotNull(zones[i].LeavesHazard);
                Assert.AreEqual("spore_cloud", zones[i].LeavesHazard.Kind);
                Assert.IsTrue(bw.W.InArena(zones[i].Pos));
            }
            Assert.GreaterOrEqual((zones[1].Pos - zones[0].Pos).magnitude, 4f - FourKit.Eps);
            Assert.GreaterOrEqual((zones[2].Pos - zones[0].Pos).magnitude, 4f - FourKit.Eps);
            Assert.GreaterOrEqual((zones[2].Pos - zones[1].Pos).magnitude, 4f - FourKit.Eps);
            // 빈틈 없음: 판정 틱에 바로 다음 대기로.
            FourKit.RunThrough(bw, r + 60);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 9.3절: 포자 안개는 동시 6개. 넘는 새 구름은 생략하고(플레이어 위치의 것이 먼저), 셋 다 생략이면 고를 수 없다.
        [TestCase(3, 3)]
        [TestCase(4, 2)]
        [TestCase(5, 1)]
        public void S06_9_3__SporeCloudSkipsBeyondSix(int existing, int expectedNew)
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternCloud);
            bw.FinishIntro();
            for (int i = 0; i < existing; i++) FourKit.AddHazard(bw, HazardKind.SporeCloud, new Vector2(1f + i, 1f), 2f, bw.Body.Id);
            FourKit.WaitPattern(bw, SporeQueenBrain.PatternCloud);
            var zones = bw.Threats.Tickets[0].Request.Zones;
            Assert.AreEqual(expectedNew, zones.Count);
            Assert.AreEqual(bw.P.Pos, zones[0].Pos);
        }

        [Test]
        public void S06_9_3__SporeCloudNotEligibleAtSix()
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternCloud);
            bw.FinishIntro();
            for (int i = 0; i < 6; i++) FourKit.AddHazard(bw, HazardKind.SporeCloud, new Vector2(1f + i, 1f), 2f, bw.Body.Id);
            Assert.IsFalse(bw.Ctx.Brain.Eligible(bw.Ctx, bw.Ctx.Pattern(SporeQueenBrain.PatternCloud)));
            bw.Step(600);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPatternStarted));
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            // 하나가 사라지면 다시 고를 수 있다.
            bw.W.Hazards[0].Dead = true;
            Assert.IsTrue(bw.Ctx.Brain.Eligible(bw.Ctx, bw.Ctx.Pattern(SporeQueenBrain.PatternCloud)));
        }

        // 9.3절 summon_sprouts: 땅에 금 4곳 60틱 → 새싹 4마리. 계약: NoReward, SummonerId = 몸통 Id.
        [Test]
        public void S06_9_3__SummonSproutsAfterWarn()
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternSprouts);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, SporeQueenBrain.PatternSprouts);
            Assert.AreEqual(4, bw.CountEvents(SimEventType.EnemySpawnNotice));
            for (int i = 0; i < 4; i++)
            {
                var notice = bw.EventOf(SimEventType.EnemySpawnNotice, i);
                Assert.AreEqual(r, notice.Tick);
                Assert.AreEqual(60, notice.A);
                Assert.AreEqual(Ids.Enemy.SporeSeedling, notice.Id);
            }
            bw.StepTo(r + 60);
            Assert.AreEqual(0, bw.Enemies.Spawned.Count);
            bw.Step(1);
            Assert.AreEqual(4, bw.Enemies.Spawned.Count);
            Assert.AreEqual(4, bw.CountEvents(SimEventType.BossMinionSpawned));
            for (int i = 0; i < 4; i++)
            {
                var spec = bw.Enemies.Spawned[i];
                Assert.AreEqual(r + 60, bw.Enemies.SpawnTicks[i]);
                Assert.AreEqual(Ids.Enemy.SporeSeedling, spec.DefId);
                Assert.IsTrue(spec.NoReward);
                Assert.AreEqual(bw.Body.Id, spec.SummonerId);
                Assert.AreEqual(bw.EventOf(SimEventType.EnemySpawnNotice, i).Pos, spec.Pos, "금이 간 자리에 나온다");
                Assert.IsTrue(bw.W.InArena(spec.Pos));
            }
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State, "빈틈 없음");
        }

        // 9.3절: 새싹은 전장 8마리까지, 넘는 만큼 생략. 계약: 상한은 SummonerId 기준(다른 출처의 적은 세지 않는다).
        [TestCase(6, 0, 2)]
        [TestCase(5, 0, 3)]
        [TestCase(0, 8, 4)]
        [TestCase(6, 8, 2)]
        public void S06_9_3__SproutCapCountsOwnSummonsOnly(int own, int foreign, int expected)
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternSprouts);
            bw.FinishIntro();
            for (int i = 0; i < own; i++) FourKit.AddMinion(bw, Ids.Enemy.SporeSeedling, bw.Body.Id);
            for (int i = 0; i < foreign; i++) FourKit.AddMinion(bw, Ids.Enemy.SporeSeedling, 0);
            int r = FourKit.WaitPattern(bw, SporeQueenBrain.PatternSprouts);
            Assert.AreEqual(expected, bw.CountEvents(SimEventType.EnemySpawnNotice));
            FourKit.RunThrough(bw, r + 60);
            Assert.AreEqual(expected, bw.Enemies.Spawned.Count);
            Assert.AreEqual(own + expected, bw.Ctx.CountSummoned(Ids.Enemy.SporeSeedling));
        }

        [Test]
        public void S06_9_3__SproutsNotEligibleAtEight()
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternSprouts);
            bw.FinishIntro();
            for (int i = 0; i < 8; i++) FourKit.AddMinion(bw, Ids.Enemy.SporeSeedling, bw.Body.Id);
            bw.Step(600);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPatternStarted));
            Assert.AreEqual(0, bw.Enemies.Spawned.Count);
            // 죽어 가는 새싹은 세지 않는다(공통 규칙 2).
            bw.W.Enemies[1].PendingDeath = true;
            Assert.IsTrue(bw.Ctx.Brain.Eligible(bw.Ctx, bw.Ctx.Pattern(SporeQueenBrain.PatternSprouts)));
        }

        // 9.3절 leap(2단계): 플레이어 위치 그림자 circle r3.0, 72틱 → 착지(피해 2) + 8방향 탄(속도 5, 대). 빈틈 90틱.
        [Test]
        public void S06_9_3__LeapLandsAndFiresEightWay()
        {
            var bw = FourKit.Make(Id, 30, SporeQueenBrain.PatternLeap);
            bw.FinishIntro();
            Assert.IsFalse(bw.Ctx.PhaseDef.Patterns.Contains(SporeQueenBrain.PatternLeap), "1단계에는 도약이 없다");
            FourKit.ToPhase(bw, 1);
            int r = FourKit.WaitPattern(bw, SporeQueenBrain.PatternLeap);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var zone = bw.Threats.Tickets[0].Request.Zones[0];
            Vector2 shadow = bw.P.Pos;
            Assert.AreEqual(ZoneShape.Circle, zone.Shape);
            Assert.That(zone.Radius, Is.EqualTo(3f));
            Assert.AreEqual(72, zone.TelegraphTicks);
            Assert.AreEqual(2, zone.BaseDamage);
            Assert.AreEqual(shadow, zone.Pos);
            // 예고가 뜬 뒤 플레이어가 달아나도 그림자는 그 자리다.
            bw.P.Pos = new Vector2(3f, 3f);
            bw.StepTo(r + 72);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            Assert.That((bw.Body.Pos - shadow).magnitude, Is.GreaterThan(1f));
            bw.Step(1);   // 판정 틱
            Assert.That((bw.Body.Pos - shadow).magnitude, Is.LessThan(FourKit.Eps), "판정 틱에 그림자 자리로 내려앉는다");
            Assert.AreEqual(8, bw.Threats.Shots.Count);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(r + 72, bw.Threats.ShotTicks[i]);
                Assert.That((bw.Threats.Shots[i].Pos - shadow).magnitude, Is.LessThan(FourKit.Eps));
                Assert.That(bw.Threats.Shots[i].Speed, Is.EqualTo(5f));
                Assert.That(bw.Threats.Shots[i].Radius, Is.EqualTo(0.18f).Within(0.0001f));
                Assert.AreEqual(1, bw.Threats.Shots[i].BaseDamage);
            }
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[1].Dir), Is.EqualTo(45f).Within(0.01f));
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(r + 72 + 90);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 9.3절 2회차: 이 보스의 포자 안개가 0.8칸/초로 플레이어 쪽으로 흐른다. 1회차는 흐르지 않는다.
        [TestCase(30, 0f)]
        [TestCase(90, 0.8f)]
        public void S06_9_3__SecondCycleCloudDrift(int wave, float cellsPerSecond)
        {
            var bw = FourKit.Make(Id, wave, FourKit.NoPattern);
            bw.FinishIntro();
            Vector2 start = new Vector2(2f, 10f);
            var mine = FourKit.AddHazard(bw, HazardKind.SporeCloud, start, 2f, bw.Body.Id);
            var other = FourKit.AddHazard(bw, HazardKind.SporeCloud, start, 2f, 0);
            var ember = FourKit.AddHazard(bw, HazardKind.EmberStrip, start, 2f, bw.Body.Id);
            bw.Step(60);
            Assert.That(mine.Pos.x - start.x, Is.EqualTo(cellsPerSecond).Within(0.001f));
            Assert.That(mine.Pos.y, Is.EqualTo(start.y).Within(0.001f), "플레이어(12, 10) 쪽으로 곧게");
            Assert.AreEqual(start, other.Pos);
            Assert.AreEqual(start, ember.Pos);
        }

        // 공통 규칙 3: 대상 플레이어가 없으면 그 틱은 가만히.
        [Test]
        public void S06_Rule3__NoTargetNoAction()
        {
            var bw = FourKit.Make(Id, 90, null, still: false);
            bw.FinishIntro();
            bw.P.Downed = true;
            var cloud = FourKit.AddHazard(bw, HazardKind.SporeCloud, new Vector2(2f, 10f), 2f, bw.Body.Id);
            Vector2 before = bw.Body.Pos;
            bw.Step(300);
            Assert.AreEqual(before, bw.Body.Pos);
            Assert.AreEqual(new Vector2(2f, 10f), cloud.Pos);
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            Assert.AreEqual(0, bw.Enemies.Spawned.Count);
        }
    }
}
