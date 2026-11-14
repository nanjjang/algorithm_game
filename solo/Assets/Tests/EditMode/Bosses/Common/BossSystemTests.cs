using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Common
{
    /// <summary>
    /// 보스 뼈대의 틱 계약(00 문서 5.5·5.6절, 05 요약서 8.3·10절). Threats·Enemies·Player는 가짜, Run은 빈 구현
    /// (RunPhase는 테스트가 Run이 하듯 직접 옮긴다).
    /// </summary>
    public class BossSystemTests
    {
        const int IntroTicks = 72;

        // ------------------------------------------------------------ 등장·시계

        // 계약: RunPhase가 BossIntro(CombatActive 거짓)여도 Intro가 72틱에 끝나 Waiting이 된다. 시계는 한 번만 돈다.
        [Test]
        public void S06_8_3__IntroEndsAt72TicksWhileNotCombatActive()
        {
            var bw = BossWorld.Create();
            bw.Spawn(Ids.Boss.KilnBoar, 10);
            Assert.AreEqual(BossFightState.Intro, bw.Boss.State);
            Assert.AreEqual(0, bw.Boss.StateStartTick);
            Assert.AreEqual(RunPhase.BossIntro, bw.W.Run.Phase);
            Assert.IsFalse(bw.W.CombatActive);

            bw.Step(IntroTicks);   // 틱 0~71
            Assert.AreEqual(BossFightState.Intro, bw.Boss.State);
            bw.Step(1);            // 틱 72
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(IntroTicks, bw.Boss.StateStartTick);
            Assert.AreEqual(IntroTicks, bw.TickOf(SimEventType.BossIntroEnd));

            bw.Step(200);          // Run이 옮기지 않아 CombatActive가 계속 거짓이어도 등장이 다시 돌지 않는다
            Assert.AreEqual(1, bw.CountEvents(SimEventType.BossIntroEnd));
            Assert.AreEqual(1, bw.CountEvents(SimEventType.BossIntroBegin));
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPatternStarted), "패턴 고르기는 CombatActive일 때만");
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
        }

        [Test]
        public void S06_8_2__IntroBossTakesNoDamageAndDoesNotAttack()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            Assert.IsTrue(body.IsBoss);
            Assert.IsTrue(body.Has(EnemyTags.Boss));
            Assert.AreEqual(Ids.Boss.KilnBoar, body.DefId);
            Assert.That(body.Hp, Is.EqualTo(3000f));
            Assert.AreSame(body, bw.W.FindEnemy(bw.Boss.EnemyId));
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(0f));
            bw.P.Pos = body.Pos;   // 몸이 겹쳐 있어도 등장 중에는 접촉 피해가 없다
            bw.Step(IntroTicks);
            Assert.AreEqual(0, bw.Player.Hits.Count);
            bw.FinishIntro();
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(10f));
        }

        // 05 요약서 8.3절: 등장이 끝난 뒤 대기 90틱(격노 0) → 패턴 시작.
        [Test]
        public void S06_8_3__FirstPatternStartsAfterWait90()
        {
            var bw = BossWorld.Create();
            bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            Assert.AreEqual(IntroTicks, introEnd);
            bw.StepTo(introEnd + 90);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPatternStarted));
            bw.Step(1);
            Assert.AreEqual(introEnd + 90, bw.TickOf(SimEventType.BossPatternStarted));
            Assert.IsNotNull(bw.Boss.CurrentPattern);
        }

        // 05 요약서 8.2절: 뇌광창의 동작 멈춤(18틱) 동안 패턴 타이머가 멈춘다.
        [Test]
        public void S06_8_2__ActionHaltPausesWaitTimer()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            int halt = SimConst.Ticks(TestContent.Db.BossCommon.ActionHaltSeconds);
            Assert.AreEqual(18, halt);
            bw.Sys.ActionHalt(bw.W, body, halt);   // 틱 73에 건다 → 73~90 멈춤
            Vector2 before = body.Pos;
            bw.Step(halt);
            Assert.AreEqual(before, body.Pos, "멈춘 동안 움직이지 않는다");
            bw.StepTo(introEnd + 90 + halt + 1);
            Assert.AreEqual(introEnd + 90 + halt, bw.TickOf(SimEventType.BossPatternStarted));
        }

        // 공통 규칙 3: 대상 플레이어가 없으면 그 틱은 가만히.
        [Test]
        public void S06_Rule3__NoTargetNoAction()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            bw.FinishIntro();
            bw.P.Downed = true;
            Vector2 before = body.Pos;
            bw.Step(300);
            Assert.AreEqual(before, body.Pos);
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPatternStarted));
        }

        // 05 요약서 8.3절: 격노 스택은 등장이 끝난 틱부터 1200틱마다.
        [Test]
        public void S06_8_3__RageStackEventAt1200FightTicks()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain("none"));
            bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            bw.StepTo(introEnd + 1200);
            Assert.AreEqual(0, bw.Boss.RageStacks);
            bw.Step(1);
            Assert.AreEqual(1, bw.Boss.RageStacks);
            Assert.AreEqual(introEnd + 1200, bw.TickOf(SimEventType.BossRageStack));
            Assert.AreEqual(1200, bw.Boss.FightTicks);
        }

        // ------------------------------------------------------------ 패턴 실행기(가마등)

        // 공통 규칙 4: 예고 뒤 자체 타격은 판정 틱(요청 틱 + 예고 틱)과 같은 틱의 7단계. 발구르기: 54틱 뒤 12방향 고리, 빈틈 48틱.
        [Test]
        public void S06_9_1__StompRingFiresOnDetonateTick()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain(KilnBoarBrain.PatternStomp));
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            int request = introEnd + 90;
            bw.StepTo(request + 1);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var t = bw.Threats.Tickets[0];
            Assert.AreEqual(request, t.RequestTick);
            Assert.IsTrue(t.Request.CountAsOne, "보스 패턴의 구역은 상한 1개로 센다");
            Assert.AreEqual(body.Id, t.Request.OwnerEnemyId);
            Assert.AreEqual(1, t.Request.Zones.Count);
            var zone = t.Request.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, zone.Shape);
            Assert.That(zone.Radius, Is.EqualTo(2f));
            Assert.AreEqual(54, zone.TelegraphTicks);
            Assert.AreEqual(2, zone.BaseDamage);
            Assert.AreEqual(PlayerDamageSource.Telegraph, zone.DamageSource);
            Assert.AreEqual(body.Pos, zone.Pos);

            bw.StepTo(request + 54);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);
            bw.Step(1);   // 판정 틱
            Assert.AreEqual(12, bw.Threats.Shots.Count);
            foreach (int tick in bw.Threats.ShotTicks) Assert.AreEqual(request + 54, tick);
            var first = bw.Threats.Shots[0];
            Assert.That(first.Speed, Is.EqualTo(5f));
            Assert.That(first.Radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.AreEqual(1, first.BaseDamage);
            Assert.IsTrue(first.Grazeable);
            Assert.That(Vector2.Angle(first.Dir, bw.P.Pos - body.Pos), Is.LessThan(0.01f), "첫 탄은 플레이어 쪽");
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[1].Dir), Is.EqualTo(30f).Within(0.01f));
            Assert.AreEqual(bw.Threats.Shots[0].CastId, bw.Threats.Shots[11].CastId, "같은 시전은 스침 한 번");

            // 빈틈 48틱 뒤 다시 Waiting.
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(request + 54 + 48);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(KilnBoarBrain.PatternStomp, bw.Boss.LastPattern);
        }

        // 9.1절 2회차: 발구르기 고리 두 겹(둘째는 +30틱, 15도 어긋남).
        [Test]
        public void S06_9_1__SecondCycleStompFiresTwoRings()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain(KilnBoarBrain.PatternStomp));
            bw.Spawn(Ids.Boss.KilnBoar, 70);
            Assert.AreEqual(2, bw.Boss.Cycle);
            int request = bw.FinishIntro() + 90;
            bw.StepTo(request + 54 + 30 + 1);
            Assert.AreEqual(24, bw.Threats.Shots.Count);
            Assert.AreEqual(request + 54, bw.Threats.ShotTicks[0]);
            Assert.AreEqual(request + 54 + 30, bw.Threats.ShotTicks[12]);
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[12].Dir), Is.EqualTo(15f).Within(0.01f));
        }

        // 9.1절 삼연 돌진: 첫 예고 60틱, 판정 틱에 돌진 시작(16칸/초), 몸 충돌은 Dash 분류·AttackId = 몸통 Id,
        // 다음 예고(42틱)는 앞 돌진이 벽에 닿은 틱, 마지막 돌진 뒤 기절 150틱(받는 피해 ×1.5), 지나간 길에 불씨 띠.
        [Test]
        public void S06_9_1__TripleChargeTimingCollisionAndStun()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            bw.Boss.LastPattern = KilnBoarBrain.PatternStomp;   // 직전 제외 → 삼연 돌진만 남는다
            int request = introEnd + 90;
            bw.StepTo(request + 1);
            Assert.AreEqual(KilnBoarBrain.PatternTripleCharge, bw.Boss.CurrentPattern);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var band = bw.Threats.Tickets[0].Request.Zones[0];
            Assert.AreEqual(ZoneShape.Band, band.Shape);
            Assert.That(band.Width, Is.EqualTo(2f));
            Assert.AreEqual(60, band.TelegraphTicks);
            Assert.AreEqual(PlayerDamageSource.Dash, band.DamageSource, "돌진 띠의 피해는 몸 충돌이 준다");
            Assert.AreEqual(0, band.BaseDamage);
            Assert.IsNull(band.LeavesHazard);

            bw.StepTo(request + 60);
            Assert.IsFalse(body.DashActive);
            Vector2 start = body.Pos;
            bw.Step(1);   // 판정 틱 = 돌진 시작 틱
            Assert.IsTrue(body.DashActive);
            Assert.That((body.Pos - start).magnitude, Is.EqualTo(16f / 60f).Within(0.0001f));
            Assert.AreEqual(2, body.DashBaseDamage);
            Assert.AreEqual(BossFightState.Execute, bw.Boss.State);

            int guard = 0;
            while (body.DashActive && guard++ < 200) bw.Step(1);
            int wallTick = bw.W.Tick - 1;
            Assert.That(body.Pos.y, Is.EqualTo(bw.W.ArenaHeight - body.BodyRadius).Within(0.001f), "벽에서 멈춘다");
            Assert.AreEqual(1, bw.Player.Hits.Count, "돌진 한 번에 한 번만 맞는다");
            var hit = bw.Player.Hits[0];
            Assert.AreEqual(PlayerDamageSource.Dash, hit.Source);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(body.Id, hit.SourceEnemyId);
            Assert.AreEqual(2, hit.DamageCells);
            Assert.GreaterOrEqual(bw.Player.HitTicks[0], request + 60);

            // 불씨 띠: 폭 1.0, 3초, 접촉 1.5초, 피해 1.
            Assert.Greater(bw.Threats.HazardSpecs.Count, 0);
            foreach (var h in bw.Threats.HazardSpecs)
            {
                Assert.AreEqual(HazardKind.EmberStrip, h.Kind);
                Assert.AreEqual(body.Id, h.OwnerEnemyId);
                Assert.That(h.Width, Is.EqualTo(1f));
                Assert.AreEqual(180, h.DurationTicks);
                Assert.AreEqual(90, h.ContactCooldownTicks);
                Assert.AreEqual(1, h.BaseDamage);
                Assert.LessOrEqual(h.Length, 1.0001f);
            }
            int embersOfFirstDash = bw.Threats.HazardSpecs.Count;

            // 다음 예고는 벽에 닿은 틱에 청한다(0.7초 = 42틱).
            Assert.AreEqual(2, bw.Threats.Tickets.Count);
            Assert.AreEqual(wallTick, bw.Threats.Tickets[1].RequestTick);
            Assert.AreEqual(42, bw.Threats.Tickets[1].Request.Zones[0].TelegraphTicks);
            Assert.AreEqual(BossFightState.Execute, bw.Boss.State);

            // 새 돌진 예고가 뜨면 그 길 2.5칸 안의 불씨가 꺼진다(같은 줄을 되돌아오므로 전부).
            bw.Step(1);
            Assert.AreEqual(embersOfFirstDash, bw.CountEvents(SimEventType.HazardExpired));

            bw.StepTo(wallTick + 42);
            Assert.IsFalse(body.DashActive);
            bw.Step(1);
            Assert.IsTrue(body.DashActive, "둘째 돌진도 판정 틱에 시작");

            guard = 0;
            while (bw.Boss.State != BossFightState.Recovery && guard++ < 1000) bw.Step(1);
            int stunTick = bw.W.Tick - 1;
            Assert.AreEqual(3, bw.Threats.Tickets.Count, "1단계는 삼연 돌진");
            Assert.AreEqual(3, bw.Player.Hits.Count);
            Assert.IsFalse(body.DashActive);
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(15f), "기절 중 받는 피해 ×1.5");
            bw.StepTo(stunTick + 150);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(10f));
        }

        // 9.1절 낙석: 원 6개(1개는 플레이어 위치, 5개는 플레이어와 ≥ 3.0, 서로 ≥ 4.0, 벽 ≥ 1.5, 32회 시도 뒤 생략), 판정 틱에 바위.
        [Test]
        public void S06_9_1__RockfallPlacementAndBoulders()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain(KilnBoarBrain.PatternRockfall));
            bw.Spawn(Ids.Boss.KilnBoar, 10);
            bw.Boss.Phase = 1;   // 낙석은 2단계 패턴
            int request = bw.FinishIntro() + 90;
            bw.StepTo(request + 1);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var zones = bw.Threats.Tickets[0].Request.Zones;
            Assert.GreaterOrEqual(zones.Count, 1);
            Assert.LessOrEqual(zones.Count, 6);
            Assert.AreEqual(bw.P.Pos, zones[0].Pos, "첫 원은 플레이어 위치");
            for (int i = 0; i < zones.Count; i++)
            {
                Assert.AreEqual(ZoneShape.Circle, zones[i].Shape);
                Assert.That(zones[i].Radius, Is.EqualTo(1.5f));
                Assert.AreEqual(60, zones[i].TelegraphTicks);
                Assert.AreEqual(1, zones[i].BaseDamage);
                if (i == 0) continue;
                Assert.GreaterOrEqual((zones[i].Pos - bw.P.Pos).magnitude, 3.0f);
                Assert.IsTrue(bw.W.InArena(zones[i].Pos, 1.5f), "벽에서 1.5칸 이상");
                for (int k = 1; k < i; k++) Assert.GreaterOrEqual((zones[i].Pos - zones[k].Pos).magnitude, 4.0f);
            }

            bw.StepTo(request + 60);
            Assert.AreEqual(0, bw.Threats.ObstacleSpecs.Count);
            bw.Step(1);
            Assert.AreEqual(zones.Count, bw.Threats.ObstacleSpecs.Count);
            var rock = bw.Threats.ObstacleSpecs[0];
            Assert.AreEqual("boulder", rock.Kind);
            Assert.That(rock.Radius, Is.EqualTo(0.5f));
            Assert.AreEqual(240, rock.DurationTicks);
            Assert.That(rock.Hp, Is.EqualTo(0f));
            Assert.IsTrue(rock.BlocksPlayerShots);
            Assert.IsTrue(rock.BlocksEnemyShots);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State, "낙석은 빈틈이 없다");
        }

        // 9.1절: 돌진이 지나간 바위는 깨져 파편 6발(60도, 속도 6, 대 탄, 피해 1).
        [Test]
        public void S06_9_1__ChargeBreaksBoulderIntoSixShards()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            bw.Boss.LastPattern = KilnBoarBrain.PatternStomp;
            bw.StepTo(introEnd + 90 + 60);
            // 돌진 길(몸통 → 플레이어) 위에 바위 하나.
            Vector2 dir = (bw.P.Pos - body.Pos).normalized;
            var spec = new ObstacleSpec { Kind = "boulder", Pos = body.Pos + dir * 1.5f, Radius = 0.5f, DurationTicks = 240 };
            var rock = bw.Threats.SpawnObstacle(bw.W, spec);
            int shotsBefore = bw.Threats.Shots.Count;
            int guard = 0;
            while (!rock.Dead && guard++ < 60) bw.Step(1);
            Assert.IsTrue(rock.Dead);
            Assert.AreEqual(1, bw.CountEvents(SimEventType.ObstacleDestroyed));
            Assert.AreEqual(shotsBefore + 6, bw.Threats.Shots.Count);
            var a = bw.Threats.Shots[shotsBefore];
            var b = bw.Threats.Shots[shotsBefore + 1];
            Assert.That(a.Speed, Is.EqualTo(6f));
            Assert.That(a.Radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.AreEqual(1, a.BaseDamage);
            Assert.That(Vector2.Angle(a.Dir, b.Dir), Is.EqualTo(60f).Within(0.01f));
            Assert.AreEqual(rock.Pos, a.Pos);
        }

        // 8.3절: 첫 예고가 R5로 막히면 3틱마다 다시 보고, 120틱 넘게 막히면 같은 단계의 다른 패턴으로 다시 고른다.
        [Test]
        public void S06_8_3__R5BlockedRetriesAndRepicksAfter120()
        {
            var bw = BossWorld.Create();
            bw.Spawn(Ids.Boss.KilnBoar, 10);
            int introEnd = bw.FinishIntro();
            bw.Threats.EscapeOk = false;
            bw.Boss.LastPattern = KilnBoarBrain.PatternStomp;
            int start = introEnd + 90;
            bw.StepTo(start + 1);
            Assert.AreEqual(BossFightState.R5Blocked, bw.Boss.State);
            Assert.AreEqual(KilnBoarBrain.PatternTripleCharge, bw.Boss.CurrentPattern);
            Assert.AreEqual(0, bw.Threats.Tickets.Count, "막힌 동안 예고를 청하지 않는다");
            bw.StepTo(start + 120);
            Assert.AreEqual(KilnBoarBrain.PatternTripleCharge, bw.Boss.CurrentPattern);
            bw.Step(1);
            Assert.AreEqual(KilnBoarBrain.PatternStomp, bw.Boss.CurrentPattern, "120틱 뒤 다른 패턴으로");
            Assert.AreEqual(0, bw.Threats.Tickets.Count);

            // 막힌 동안에는 3틱마다 다시 본다(r5_retry_seconds 0.05초).
            Assert.AreEqual(start, bw.Threats.EscapeCheckTicks[0]);
            Assert.AreEqual(start + 3, bw.Threats.EscapeCheckTicks[1]);
            Assert.AreEqual(start + 6, bw.Threats.EscapeCheckTicks[2]);
            Assert.AreEqual(start + 117, bw.Threats.EscapeCheckTicks[39]);

            // 길이 열리면 다음 재검사(3틱 안)에 예고가 뜬다.
            bw.Threats.EscapeOk = true;
            bw.Step(3);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);
        }

        // ------------------------------------------------------------ 단계·사망

        // 8.3절: 단계 전환 36틱(미판정 예고 삭제), 전환 중에도 피해는 받는다.
        [Test]
        public void S06_8_3__PhaseTransition36TicksCancelsTelegraphs()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain(KilnBoarBrain.PatternStomp));
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            int request = bw.FinishIntro() + 90;
            bw.StepTo(request + 10);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);
            body.Hp = body.MaxHp * 60f / 100f;   // 정확히 60% → 2단계(경계 포함)
            int t = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(BossFightState.PhaseTransition, bw.Boss.State);
            Assert.AreEqual(1, bw.Boss.Phase);
            Assert.AreEqual(t, bw.Boss.StateStartTick);
            Assert.AreEqual(t, bw.TickOf(SimEventType.BossPhaseChanged));
            Assert.AreEqual(1, bw.EventOf(SimEventType.BossPhaseChanged).A);
            Assert.IsTrue(bw.Threats.Tickets[0].Cancelled, "미판정 예고 삭제");
            Assert.Contains(body.Id, bw.Threats.CancelledOwners);
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(10f));
            Assert.AreEqual(3, bw.Ctx.Pattern(KilnBoarBrain.PatternTripleCharge).Steps[1].Repeat, "2단계 덮어쓰기 적용");

            bw.StepTo(t + 36);
            Assert.AreEqual(BossFightState.PhaseTransition, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(0, bw.Threats.Shots.Count, "전환 중에는 공격이 없다");
            Assert.AreEqual(1, bw.CountEvents(SimEventType.BossPhaseChanged));
        }

        // 8.3절: 한 틱에 두 경계를 넘으면 마지막 단계로 한 번만 전환한다.
        [Test]
        public void S06_8_3__DoubleBoundaryTransitionsOnce()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.FinishIntro();
            body.Hp = body.MaxHp * 0.2f;
            bw.Step(1);
            Assert.AreEqual(2, bw.Boss.Phase);
            bw.Step(100);
            Assert.AreEqual(1, bw.CountEvents(SimEventType.BossPhaseChanged));
            Assert.AreEqual(2, bw.EventOf(SimEventType.BossPhaseChanged).A);
        }

        // 8.3절 사망: State = Dead, BossKilled, 남은 졸개 제거(minionsOnly), 남은 적 탄 전부 제거. HpMin 추적.
        [Test]
        public void S06_8_3__DeathClearsMinionsAndShots()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            bw.FinishIntro();
            body.Hp = 1234f;
            bw.Step(1);
            Assert.That(bw.Boss.HpMin, Is.EqualTo(1234f));
            body.Hp = 0f;
            body.PendingDeath = true;
            int t = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(BossFightState.Dead, bw.Boss.State);
            Assert.AreEqual(t, bw.TickOf(SimEventType.BossKilled));
            Assert.AreEqual(Ids.Boss.KilnBoar, bw.EventOf(SimEventType.BossKilled).Id);
            Assert.AreEqual(body.Id, bw.EventOf(SimEventType.BossKilled).EntityId);
            CollectionAssert.AreEqual(new[] { true }, bw.Enemies.RemoveAllArgs);
            CollectionAssert.AreEqual(new[] { t }, bw.Threats.ClearShotsTicks);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.RevengeKill));
            Assert.That(bw.Boss.HpMin, Is.EqualTo(0f));
            bw.Step(50);
            Assert.AreEqual(1, bw.CountEvents(SimEventType.BossKilled));
            Assert.IsNotNull(bw.W.Boss, "걷는 것은 Run이 Dismiss로 한다");
        }

        // D-45: 같은 틱에 플레이어가 먼저 쓰러져 판이 끝났으면(RunEnded) BossKilled·RevengeKill을 내지 않는다.
        [Test]
        public void S07_D45__NoBossKilledWhenRunAlreadyEnded()
        {
            var bw = BossWorld.Create();
            bw.W.Run.RevengeActive = true;
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.FinishIntro();
            body.Hp = 0f;
            bw.W.Run.Phase = RunPhase.RunEnded;   // Player.Stage10 → Run.OnPlayerDowned가 먼저 판을 끝냈다
            bw.Step(1);
            Assert.AreEqual(BossFightState.Dead, bw.Boss.State);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossKilled));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.RevengeKill));
        }

        // 10.3절: 복수 꼬리표·RevengeKill은 w.Run.RevengeActive(판 안의 실효 값)로 본다 — Config.RevengePending이 아니다.
        [Test]
        public void S06_10_3__RevengeTagAndKillFollowRevengeActive()
        {
            // 판 중에 첫 만남을 치른 판: 출발 때는 복수 중이 아니었지만(RevengePending = false) RevengeActive가 켜져 있다.
            var bw = BossWorld.Create();
            bw.W.Run.Config = new RunConfig { RevengePending = false, PhantomFirst = true };
            bw.W.Run.PhantomFirstDone = true;
            bw.W.Run.RevengeActive = true;
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            Assert.IsFalse(bw.Boss.FirstEncounter);
            Assert.AreEqual(1, bw.EventOf(SimEventType.BossIntroBegin).A, "복수 꼬리표");
            Assert.AreEqual(Ids.Boss.MirrorPhantom, bw.EventOf(SimEventType.BossIntroBegin).Id);
            bw.FinishIntro();
            body.Hp = 0f;
            int t = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(t, bw.TickOf(SimEventType.BossKilled));
            Assert.AreEqual(t, bw.TickOf(SimEventType.RevengeKill));
            Assert.AreEqual(body.Id, bw.EventOf(SimEventType.RevengeKill).EntityId);

            // 복수 중이 아닌 판(이미 복수를 끝낸 프로필 등): 꼬리표도 RevengeKill도 없다. Config 값은 보지 않는다.
            var bw2 = BossWorld.Create();
            bw2.W.Run.Config = new RunConfig { RevengePending = true };
            bw2.W.Run.RevengeActive = false;
            var body2 = bw2.Spawn(Ids.Boss.MirrorPhantom, 20);
            Assert.AreEqual(0, bw2.EventOf(SimEventType.BossIntroBegin).A);
            bw2.FinishIntro();
            body2.Hp = 0f;
            bw2.Step(1);
            Assert.AreEqual(1, bw2.CountEvents(SimEventType.BossKilled));
            Assert.AreEqual(0, bw2.CountEvents(SimEventType.RevengeKill));

            // 웨이브 80의 허상(2회차)과 웨이브 10의 가마등은 복수 대상이 아니다.
            var bw3 = BossWorld.Create();
            bw3.W.Run.RevengeActive = true;
            bw3.Spawn(Ids.Boss.MirrorPhantom, 80);
            Assert.AreEqual(0, bw3.EventOf(SimEventType.BossIntroBegin).A);
            var bw4 = BossWorld.Create();
            bw4.W.Run.RevengeActive = true;
            bw4.Spawn(Ids.Boss.KilnBoar, 10);
            Assert.AreEqual(0, bw4.EventOf(SimEventType.BossIntroBegin).A);
        }

        // 계약: Dismiss 뒤 w.Boss == null이고 몸통이 없다.
        [Test]
        public void S06_Dismiss__ClearsBossAndBody()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            bw.FinishIntro();
            bw.Step(120);
            bw.Sys.Dismiss(bw.W);
            Assert.IsNull(bw.W.Boss);
            Assert.IsTrue(body.Dead);
            Assert.IsFalse(body.DashActive);
            bw.Step(1);
            Assert.IsFalse(bw.W.Enemies.Exists(e => e.IsBoss), "몸통이 목록에서 빠진다");
            Assert.IsNull(bw.W.FindEnemy(body.Id));
            bw.Step(10);   // 보스가 없으면 아무 일도 없다
            bw.Sys.Dismiss(bw.W);
            Assert.IsNull(bw.W.Boss);
        }

        // 05 요약서 4절·D-63: 접촉 피해는 종별 값(가마등 2), 같은 적 1.5초에 1회. QueueHit이 false면 쿨다운을 쓰지 않는다.
        [Test]
        public void S06_8_2__ContactCooldownConsumedOnlyWhenQueued()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.KilnBoar, () => new ForcedPatternBrain("none"));
            var body = bw.Spawn(Ids.Boss.KilnBoar, 10);
            bw.FinishIntro();
            bw.P.Pos = body.Pos + new Vector2(0.5f, 0f);
            bw.Player.QueueResult = false;   // 무적 중
            bw.Step(3);
            Assert.AreEqual(3, bw.Player.Hits.Count, "false면 쿨다운을 쓰지 않아 매 틱 다시 올린다");
            bw.Player.QueueResult = true;
            int t = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(4, bw.Player.Hits.Count);
            var hit = bw.Player.Hits[3];
            Assert.AreEqual(PlayerDamageSource.Contact, hit.Source);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(2, hit.DamageCells);
            bw.StepTo(t + 90);
            Assert.AreEqual(4, bw.Player.Hits.Count, "1.5초(90틱) 안에는 다시 없다");
            bw.Step(1);
            Assert.AreEqual(5, bw.Player.Hits.Count);
        }
    }
}
