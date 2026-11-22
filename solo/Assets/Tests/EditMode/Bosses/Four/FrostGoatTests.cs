using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>흰뿔(05 요약서 9.5절). Threats·Enemies·Player는 가짜(G07의 BossWorld).</summary>
    public class FrostGoatTests
    {
        const string Id = Ids.Boss.FrostGoat;
        const string PillarKind = "ice_pillar";

        // 9.5절: 속도 3.0, 플레이어 피격 원 중심이 서리밭 안이면 2.4, 나가는 틱에 즉시 3.0.
        [Test]
        public void S06_9_5__MoveSpeedOnFrost()
        {
            var bw = FourKit.Make(Id, 50, FourKit.NoPattern, still: false);
            bw.P.Pos = new Vector2(23f, bw.Body.Pos.y);
            bw.FinishIntro();
            float x = bw.Body.Pos.x;
            bw.Step(10);
            Assert.That(bw.Body.Pos.x - x, Is.EqualTo(10 * 3.0f / 60f).Within(0.001f));

            // 보스가 아니라 플레이어가 서리밭 안일 때 느려진다.
            var frost = FourKit.AddHazard(bw, HazardKind.FrostField, bw.P.Pos + new Vector2(0f, 2f), 2.5f, bw.Body.Id);
            x = bw.Body.Pos.x;
            bw.Step(10);
            Assert.That(bw.Body.Pos.x - x, Is.EqualTo(10 * 2.4f / 60f).Within(0.001f));

            // 플레이어가 서리밭을 나가는 틱에 바로 3.0.
            bw.P.Pos += new Vector2(0f, -1f);
            Assert.Greater((bw.P.Pos - frost.Pos).magnitude, frost.Radius);
            Vector2 before = bw.Body.Pos;
            bw.Step(1);
            Assert.That((bw.Body.Pos - before).magnitude, Is.EqualTo(3.0f / 60f).Within(0.0005f));
        }

        // 9.5절 Eligible: 서리밭 안 플레이어에게 숨결 금지, 뿔 돌진 바로 뒤 숨결 금지, 기둥 6개면 기둥 금지.
        [Test]
        public void S06_9_5__EligibleRules()
        {
            var bw = FourKit.Make(Id, 50, null);
            bw.FinishIntro();
            var c = bw.Ctx;
            var breath = c.Pattern(FrostGoatBrain.PatternBreath);
            var pillar = c.Pattern(FrostGoatBrain.PatternPillar);
            Assert.IsTrue(c.Brain.Eligible(c, breath));
            Assert.IsTrue(c.Brain.Eligible(c, pillar));
            Assert.IsTrue(c.Brain.Eligible(c, c.Pattern(FrostGoatBrain.PatternSwing)));
            Assert.IsTrue(c.Brain.Eligible(c, c.Pattern(FrostGoatBrain.PatternCharge)));

            var frost = FourKit.AddHazard(bw, HazardKind.FrostField, bw.P.Pos, 2.5f, bw.Body.Id);
            Assert.IsFalse(c.Brain.Eligible(c, breath), "플레이어가 서리밭 안");
            frost.Dead = true;
            Assert.IsTrue(c.Brain.Eligible(c, breath));

            bw.Boss.LastPattern = FrostGoatBrain.PatternCharge;
            Assert.IsFalse(c.Brain.Eligible(c, breath), "직전 패턴이 뿔 돌진");
            bw.Boss.LastPattern = FrostGoatBrain.PatternSwing;
            Assert.IsTrue(c.Brain.Eligible(c, breath));

            for (int i = 0; i < 5; i++) FourKit.AddObstacle(bw, PillarKind, new Vector2(2f + 3f * i, 2f), 1f, 80f);
            Assert.IsTrue(c.Brain.Eligible(c, pillar), "기둥 5개");
            FourKit.AddObstacle(bw, PillarKind, new Vector2(20f, 12f), 1f, 80f);
            Assert.IsFalse(c.Brain.Eligible(c, pillar), "기둥 6개");
        }

        // 9.5절 고르기: 숨결이 뽑혔는데 기둥이 2개 미만이면 기둥 먼저, 그다음 숨결. 그 사이 뿔 돌진은 고르지 않는다(진짜 두뇌로 600초 돌려 본다).
        [Test]
        public void S06_9_5__BreathAlwaysHasTwoPillarsFirst()
        {
            var bw = FourKit.Make(Id, 50, null);
            bw.FinishIntro();
            var brain = (FrostGoatBrain)((ForcedBrain)bw.Ctx.Brain).Inner;
            int seenBreath = 0;
            int seen = 0;
            string prev = null;
            for (int i = 0; i < 9000; i++)
            {
                bool priorityBefore = brain.BreathPriority;
                int pillarsBefore = FrostGoatBrain.PillarCount(bw.Ctx);
                bw.Step(1);
                int n = bw.CountEvents(SimEventType.BossPatternStarted);
                if (n == seen) continue;
                seen = n;
                string key = bw.EventOf(SimEventType.BossPatternStarted, n - 1).Id;
                if (key == FrostGoatBrain.PatternBreath)
                {
                    seenBreath++;
                    Assert.AreNotEqual(FrostGoatBrain.PatternCharge, prev, "뿔 돌진 바로 뒤에는 숨결이 없다");
                    Assert.IsTrue(pillarsBefore >= FrostGoatBrain.PillarsBeforeBreath || priorityBefore,
                        "숨결 전에 기둥이 2개 이상이거나, 기둥을 먼저 세운 뒤다");
                }
                if (key == FrostGoatBrain.PatternCharge) Assert.IsFalse(priorityBefore, "숨결을 미뤄 둔 동안 뿔 돌진을 고르지 않는다");
                Assert.LessOrEqual(FrostGoatBrain.PillarCount(bw.Ctx), 6);
                prev = key;
            }
            Assert.Greater(seen, 10);
            Assert.Greater(seenBreath, 0);
        }

        // 9.5절 ice_pillar: circle r1.0 × 4, 48틱, 피해 1. 배치 플레이어 둘레 2~5칸·서로 ≥ 3.0. 판정 틱에 기둥(체력 80, 반지름 1.0, 무한, 양쪽 탄 막음).
        [Test]
        public void S06_9_5__IcePillarZonesAndObstacles()
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternPillar);
            bw.P.Pos = new Vector2(12f, 7f);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternPillar);
            var t = bw.Threats.Tickets[0];
            Assert.IsTrue(t.Request.CountAsOne, "기둥 4개는 동시 예고 상한에 1개로 센다");
            var zones = t.Request.Zones;
            Assert.AreEqual(4, zones.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(ZoneShape.Circle, zones[i].Shape);
                Assert.That(zones[i].Radius, Is.EqualTo(1f));
                Assert.AreEqual(48, zones[i].TelegraphTicks);
                Assert.AreEqual(1, zones[i].BaseDamage);
                float d = (zones[i].Pos - bw.P.Pos).magnitude;
                Assert.That(d, Is.InRange(2f - FourKit.Eps, 5f + FourKit.Eps));
                for (int k = 0; k < i; k++) Assert.GreaterOrEqual((zones[i].Pos - zones[k].Pos).magnitude, 3f - FourKit.Eps);
            }
            bw.StepTo(r + 48);
            Assert.AreEqual(0, bw.Threats.ObstacleSpecs.Count);
            bw.Step(1);
            Assert.AreEqual(4, bw.Threats.ObstacleSpecs.Count);
            for (int i = 0; i < 4; i++)
            {
                var o = bw.Threats.ObstacleSpecs[i];
                Assert.AreEqual(PillarKind, o.Kind);
                Assert.That(o.Hp, Is.EqualTo(80f), "웨이브 배율 없음");
                Assert.That(o.Radius, Is.EqualTo(1f));
                Assert.AreEqual(0, o.DurationTicks, "무한 지속");
                Assert.IsTrue(o.BlocksPlayerShots);
                Assert.IsTrue(o.BlocksEnemyShots);
                Assert.AreEqual(zones[i].Pos, o.Pos);
            }
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State, "빈틈 없음");
        }

        // 9.5절: 기둥은 전장 6개까지(넘는 만큼 생략).
        [Test]
        public void S06_9_5__IcePillarCapSix()
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternPillar);
            bw.P.Pos = new Vector2(12f, 7f);
            bw.FinishIntro();
            for (int i = 0; i < 4; i++) FourKit.AddObstacle(bw, PillarKind, new Vector2(2f + 3f * i, 1f), 1f, 80f);
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternPillar);
            FourKit.RunThrough(bw, r + 48);
            Assert.AreEqual(2, bw.Threats.ObstacleSpecs.Count);
            Assert.AreEqual(6, FrostGoatBrain.PillarCount(bw.Ctx));
            bw.Step(600);
            Assert.AreEqual(2, bw.Threats.ObstacleSpecs.Count, "6개면 기둥 패턴을 고를 수 없다");
        }

        // 9.5절: 기둥의 체력이 0이 되면 깨져 파편 8방향(45도, 속도 6, 소 0.12, 피해 1). 2회차 12방향 30도.
        [TestCase(50, 8, 45f)]
        [TestCase(110, 12, 30f)]
        public void S06_9_5__PillarShardsWhenHpZero(int wave, int count, float spread)
        {
            var bw = FourKit.Make(Id, wave, FourKit.NoPattern);
            bw.FinishIntro();
            var pillar = FourKit.AddObstacle(bw, PillarKind, new Vector2(5f, 5f), 1f, 80f);
            var cleared = FourKit.AddObstacle(bw, PillarKind, new Vector2(18f, 5f), 1f, 80f);
            bw.Step(2);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            // Threats.DamageObstacle가 하는 일(체력 0 → 치움)을 흉내 낸다.
            pillar.Hp = 0f;
            pillar.Dead = true;
            // 그냥 걷힌 기둥(체력이 남아 있다)은 파편을 쏘지 않는다.
            cleared.Dead = true;
            bw.Step(1);
            Assert.AreEqual(count, bw.Threats.Shots.Count);
            for (int i = 0; i < count; i++)
            {
                var s = bw.Threats.Shots[i];
                Assert.AreEqual(pillar.Pos, s.Pos);
                Assert.That(s.Speed, Is.EqualTo(6f));
                Assert.That(s.Radius, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.AreEqual(1, s.BaseDamage);
                Assert.AreEqual(bw.Threats.Shots[0].CastId, s.CastId);
            }
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[1].Dir), Is.EqualTo(spread).Within(0.01f));
            bw.Step(5);
            Assert.AreEqual(count, bw.Threats.Shots.Count, "한 번만 쏜다");
        }

        // 9.5절 horn_swing: fan 90도 길이 5, 60틱, 피해 2. 예고 시작 틱에 플레이어가 서리밭 안이면 72틱. 빈틈 60틱.
        [TestCase(false, 60)]
        [TestCase(true, 72)]
        public void S06_9_5__HornSwingTelegraph(bool onFrost, int ticks)
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternSwing);
            bw.FinishIntro();
            if (onFrost) FourKit.AddHazard(bw, HazardKind.FrostField, bw.P.Pos + new Vector2(1f, 0f), 2.5f, bw.Body.Id);
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternSwing);
            var zones = bw.Threats.Tickets[0].Request.Zones;
            Assert.AreEqual(1, zones.Count);
            var z = zones[0];
            Assert.AreEqual(ZoneShape.Fan, z.Shape);
            Assert.That(z.FanDegrees, Is.EqualTo(90f));
            Assert.That(z.Radius, Is.EqualTo(5f));
            Assert.AreEqual(2, z.BaseDamage);
            Assert.AreEqual(ticks, z.TelegraphTicks);
            Assert.AreEqual(bw.Body.Pos, z.Pos);
            Assert.That(Vector2.Angle(z.Dir, bw.P.Pos - bw.Body.Pos), Is.LessThan(0.01f));
            bw.StepTo(r + ticks);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(r + ticks + 60);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 9.5절 frost_breath: 예고 48틱(구역 판정 아님) → 시작 변(플레이어 방향에서 쓸 각의 절반만큼 반대)에서 초당 45도로 쓸며
        // 15틱마다 1발. 1단계 90도 8발, 2단계 120도 11발. 속도 5.5, 사거리 8, 소 0.12.
        [TestCase(0, 90f, 8)]
        [TestCase(1, 120f, 11)]
        public void S06_9_5__FrostBreathSweep(int phase, float sweep, int shots)
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternBreath);
            bw.FinishIntro();
            if (phase > 0) FourKit.ToPhase(bw, phase);
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternBreath);
            var ev = FourKit.PatternEvent(bw, FrostGoatBrain.PatternBreath, 0);
            Assert.That(ev.A, Is.EqualTo(1).Or.EqualTo(-1), "쓰는 방향(시계/반시계)");
            Assert.AreEqual((int)sweep, ev.B);
            Vector2 toPlayer = bw.P.Pos - bw.Body.Pos;
            // 예고 동안 시작 변을 바라본다.
            Assert.That(Vector2.Angle(bw.Body.Facing, toPlayer), Is.EqualTo(sweep / 2f).Within(0.01f));
            Assert.AreEqual(0, bw.Threats.Tickets.Count, "구역 판정이 아니다");
            bw.StepTo(r + 48);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            for (int k = 0; k < shots; k++)
            {
                FourKit.RunThrough(bw, r + 48 + 15 * k);
                Assert.AreEqual(k + 1, bw.Threats.Shots.Count);
                Assert.AreEqual(r + 48 + 15 * k, bw.Threats.ShotTicks[k]);
            }
            var s = bw.Threats.Shots;
            Assert.That(s[0].Speed, Is.EqualTo(5.5f));
            Assert.That(s[0].Range, Is.EqualTo(8f));
            Assert.That(s[0].Radius, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.AreEqual(1, s[0].BaseDamage);
            Assert.That(Vector2.Angle(s[0].Dir, toPlayer), Is.EqualTo(sweep / 2f).Within(0.01f), "첫 발 = 시작 변");
            // 15틱마다 11.25도씩(초당 45도) 플레이어 쪽으로 쓸어 간다.
            Assert.That(Vector2.Angle(s[0].Dir, s[1].Dir), Is.EqualTo(11.25f).Within(0.01f));
            Assert.Less(Vector2.Angle(s[1].Dir, toPlayer), Vector2.Angle(s[0].Dir, toPlayer));
            Assert.That(Vector2.Angle(s[0].Dir, s[shots - 1].Dir), Is.EqualTo(11.25f * (shots - 1)).Within(0.02f));
            // 쓰는 시간(120틱 / 160틱) 뒤 빈틈 72틱.
            int duration = Mathf.RoundToInt(sweep / 45f * 60f);
            bw.StepTo(r + 48 + duration);
            Assert.AreEqual(shots, bw.Threats.Shots.Count);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
        }

        // 9.5절 horn_charge + 계약: 띠 폭 1.6·54틱, 돌진 시작 틱 = 요청 틱 + 54, 몸 충돌 Dash·AttackId = 몸통 Id·피해 2,
        // 부딪힌 기둥 파괴 + 파편, 벽에 닿고 빈틈 90틱.
        [Test]
        public void S06_9_5__HornChargeBreaksPillar()
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternCharge);
            var body = bw.Body;
            bw.FinishIntro();
            Vector2 start = body.Pos;
            Vector2 dir = (bw.P.Pos - start).normalized;
            var pillar = FourKit.AddObstacle(bw, PillarKind, start + dir * 3f, 1f, 80f);
            var aside = FourKit.AddObstacle(bw, PillarKind, start + dir * 3f + new Vector2(-dir.y, dir.x) * 5f, 1f, 80f);
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternCharge);
            var t = bw.Threats.Tickets[0];
            Assert.AreEqual(r, t.RequestTick);
            var z = t.Request.Zones[0];
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.That(z.Width, Is.EqualTo(1.6f));
            Assert.AreEqual(54, z.TelegraphTicks);
            Assert.AreEqual(0, z.BaseDamage);
            Assert.AreEqual(PlayerDamageSource.Dash, z.DamageSource);
            Assert.AreEqual(start, z.Pos);
            Assert.That(Vector2.Angle(z.Dir, dir), Is.LessThan(0.01f));

            bw.StepTo(r + 54);
            Assert.IsFalse(body.DashActive);
            Assert.AreEqual(start, body.Pos);
            bw.Step(1);   // 판정 틱: 이 틱부터 몸이 움직인다
            Assert.IsTrue(body.DashActive);
            Assert.AreEqual(2, body.DashBaseDamage);
            Assert.That((body.Pos - start).magnitude, Is.EqualTo(16f / 60f).Within(0.001f));

            int end = FourKit.RunDash(bw);
            Assert.IsTrue(pillar.Dead, "지나간 기둥은 깨진다");
            Assert.IsFalse(aside.Dead);
            Assert.AreEqual(8, bw.Threats.Shots.Count);
            Assert.AreEqual(pillar.Pos, bw.Threats.Shots[0].Pos);
            Assert.AreEqual(1, bw.Threats.Shots[0].BaseDamage);
            Assert.AreEqual(1, FourKit.CountHits(bw, PlayerDamageSource.Dash));
            var hit = FourKit.FirstHit(bw, PlayerDamageSource.Dash);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(body.Id, hit.SourceEnemyId);
            Assert.AreEqual(2, hit.DamageCells);
            // 벽까지 달렸다.
            float toWall = BossRules.DistanceToWall(bw.W, body.Pos, dir, body.BodyRadius);
            Assert.That(toWall, Is.LessThan(0.001f));

            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(end + 90);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            bw.Step(3);
            Assert.AreEqual(8, bw.Threats.Shots.Count, "돌진이 깬 기둥은 파편을 한 번만 쏜다");
        }

        // 9.5절 frost_field(2단계): circle r2.5 × 3(1개 플레이어 위치, 서로 ≥ 5.0), 60틱, 피해 0.
        // 판정 틱에 서리밭 8초(480틱): Threats.SpawnHazard, 느려지기만(SlowOnly), 이동 속도 ×0.7 = −30%.
        [Test]
        public void S06_9_5__FrostFieldHazards()
        {
            var bw = FourKit.Make(Id, 50, FrostGoatBrain.PatternField);
            bw.P.Pos = new Vector2(12f, 7f);
            bw.FinishIntro();
            Assert.IsFalse(bw.Ctx.PhaseDef.Patterns.Contains(FrostGoatBrain.PatternField), "1단계에는 서리밭이 없다");
            FourKit.ToPhase(bw, 1);
            int r = FourKit.WaitPattern(bw, FrostGoatBrain.PatternField);
            var zones = bw.Threats.Tickets[0].Request.Zones;
            Assert.AreEqual(3, zones.Count);
            Assert.AreEqual(bw.P.Pos, zones[0].Pos);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(ZoneShape.Circle, zones[i].Shape);
                Assert.That(zones[i].Radius, Is.EqualTo(2.5f));
                Assert.AreEqual(60, zones[i].TelegraphTicks);
                Assert.AreEqual(0, zones[i].BaseDamage);
                Assert.IsNull(zones[i].LeavesHazard, "서리밭은 두뇌가 SpawnHazard로 깐다(두 번 깔리지 않게)");
                for (int k = 0; k < i; k++) Assert.GreaterOrEqual((zones[i].Pos - zones[k].Pos).magnitude, 5f - FourKit.Eps);
            }
            bw.StepTo(r + 60);
            Assert.AreEqual(0, bw.Threats.HazardSpecs.Count);
            bw.Step(1);
            Assert.AreEqual(3, bw.Threats.HazardSpecs.Count);
            for (int i = 0; i < 3; i++)
            {
                var h = bw.Threats.HazardSpecs[i];
                Assert.AreEqual(HazardKind.FrostField, h.Kind);
                Assert.IsTrue(h.SlowOnly);
                Assert.That(h.SlowPercent, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.AreEqual(480, h.DurationTicks);
                Assert.That(h.Radius, Is.EqualTo(2.5f));
                Assert.AreEqual(ZoneShape.Circle, h.Shape);
                Assert.AreEqual(0, h.MaxCount, "상한 없음");
                Assert.AreEqual(bw.Body.Id, h.OwnerEnemyId);
                Assert.AreEqual(zones[i].Pos, h.Pos);
            }
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State, "빈틈 없음");
            Assert.IsTrue(FrostGoatBrain.InFrost(bw.W, bw.P.Pos));
        }

        // 공통 규칙 3: 대상 플레이어가 없으면 그 틱은 가만히.
        [Test]
        public void S06_Rule3__NoTargetNoAction()
        {
            var bw = FourKit.Make(Id, 50, null, still: false);
            bw.FinishIntro();
            bw.P.Downed = true;
            Vector2 before = bw.Body.Pos;
            bw.Step(300);
            Assert.AreEqual(before, bw.Body.Pos);
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
        }
    }
}
