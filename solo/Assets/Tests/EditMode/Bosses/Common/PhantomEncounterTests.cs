using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Common
{
    /// <summary>
    /// 허상: 첫 만남의 시계(05 요약서 10.2절, 00 문서 5.5절 표)와 복사 사격(9.2절).
    /// 시계의 주인은 Bosses 하나다 — 테스트는 Run이 하듯 RunPhase만 옮기고, 틱은 BossState에서 읽는다.
    /// </summary>
    public class PhantomEncounterTests
    {
        const int IntroTicks = 72;

        static BossWorld FirstEncounter(out EnemyState body, out int introEnd)
        {
            var bw = BossWorld.Create();
            body = bw.Spawn(Ids.Boss.MirrorPhantom, 10, phantomFirst: true);
            introEnd = bw.FinishIntro();
            return bw;
        }

        // 10.2절: 처음부터 격노 5스택(대기 45틱)·3단계 행동, 최종 격노·단계 전환·졸개 없음.
        [Test]
        public void S06_10_2__FirstEncounterStartsAtRage5Phase3()
        {
            EnemyState body;
            int introEnd;
            var bw = FirstEncounter(out body, out introEnd);
            Assert.AreEqual(IntroTicks, introEnd);
            Assert.IsTrue(bw.Boss.FirstEncounter);
            Assert.AreEqual(5, bw.Boss.RageStacks);
            Assert.AreEqual(2, bw.Boss.Phase, "3단계 = Def.Phases의 첨자 2");
            Assert.AreEqual(45, bw.Ctx.WaitLeft);
            Assert.AreEqual(0, bw.EventOf(SimEventType.BossIntroBegin).A, "첫 만남에는 복수 꼬리표가 없다");

            // 45틱 대기 뒤 3단계 패턴(mimic_art 또는 bloom_mimic)을 고른다.
            bw.StepTo(introEnd + 45 + 1);
            Assert.AreEqual(introEnd + 45, bw.TickOf(SimEventType.BossPatternStarted));
            string key = bw.EventOf(SimEventType.BossPatternStarted).Id;
            Assert.IsTrue(key == MirrorPhantomBrain.PatternMimicArt || key == MirrorPhantomBrain.PatternBloomMimic);

            // 30초(1800틱)가 지나도 졸개가 없고, 격노 스택 이벤트·최종 격노·단계 전환이 없다.
            body.Hp = 2100f;   // 25% 경계(1000)는 바닥(2000) 아래라 닿지 않는다. 60% 아래로 내려가도 전환이 없어야 한다
            bw.StepTo(introEnd + 1900);
            Assert.AreEqual(0, bw.Enemies.Spawned.Count);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossRageStack));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossFinalRage));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossPhaseChanged));
            Assert.AreEqual(5, bw.Boss.RageStacks);
            Assert.IsFalse(bw.Boss.FinalRage);
        }

        // 10.2절·5.5절 표: 체력 바닥(50%) → Reflection(진입 틱에 InputLocked, 적 탄 전부 제거) → +114틱 ForceDown(Scripted)
        // → 180틱 뒤 Collapse → 72틱에 대사 → 240틱 뒤 CollapseDone(InputLocked 해제). QueueHit은 쓰지 않는다. AttackLocked는 건드리지 않는다.
        [Test]
        public void S06_10_2__ReflectionCollapseTimeline()
        {
            EnemyState body;
            int introEnd;
            var bw = FirstEncounter(out body, out introEnd);
            bw.P.Pos = new Vector2(2f, 12f);   // 몸이 닿지 않는 자리
            bw.P.AttackLocked = true;          // Run의 것 — 이 모듈이 바꾸지 않는지 본다
            bw.Step(30);
            Assert.IsFalse(bw.P.InputLocked);

            body.Hp = body.HpFloor;            // 2000 = 50%
            int r = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(BossFightState.Reflection, bw.Boss.State);
            Assert.AreEqual(r, bw.Boss.StateStartTick);
            Assert.AreEqual(r, bw.TickOf(SimEventType.PhantomMirrorCrack));
            Assert.AreEqual(r, bw.TickOf(SimEventType.PhantomReflection));
            Assert.IsTrue(bw.P.InputLocked, "진입 틱에 입력 잠금");
            CollectionAssert.AreEqual(new[] { r }, bw.Threats.ClearAllTicks, "진입 틱에 예고·적 탄 전부 제거");
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, Vector2.zero, Vector2.zero), Is.EqualTo(0f));
            bw.W.Run.Phase = RunPhase.PhantomFinale;   // Run이 같은 틱 11단계에 옮긴다
            int hitsBefore = bw.Player.Hits.Count;
            int shotsBefore = bw.Threats.Shots.Count;
            int ticketsBefore = bw.Threats.Tickets.Count;

            bw.StepTo(r + 114);
            Assert.AreEqual(0, bw.Player.ForceDownTicks.Count);
            Assert.IsFalse(bw.P.Downed);
            bw.Step(1);
            CollectionAssert.AreEqual(new[] { r + 114 }, bw.Player.ForceDownTicks, "되비침 +114틱에 쓰러짐");
            Assert.AreEqual(PlayerDamageSource.Scripted, bw.Player.ForceDownSources[0]);
            Assert.IsTrue(bw.P.Downed);
            Assert.AreEqual(BossFightState.Reflection, bw.Boss.State);

            // 만개 연출 24/54/84틱(판정 없음: 탄·예고를 만들지 않는다).
            Assert.AreEqual(3, CountBloom(bw));
            Assert.AreEqual(r + 24, BloomTick(bw, 0));
            Assert.AreEqual(r + 54, BloomTick(bw, 1));
            Assert.AreEqual(r + 84, BloomTick(bw, 2));

            bw.StepTo(r + 180);
            Assert.AreEqual(BossFightState.Reflection, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Collapse, bw.Boss.State, "되비침 180틱");
            Assert.AreEqual(r + 180, bw.Boss.StateStartTick);
            Assert.AreEqual(r + 180, bw.TickOf(SimEventType.PhantomCollapse));
            bw.W.Run.Phase = RunPhase.PhantomDowned;

            bw.StepTo(r + 180 + 72);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.PhantomMasterLine));
            bw.Step(1);
            Assert.AreEqual(r + 180 + 72, bw.TickOf(SimEventType.PhantomMasterLine), "쓰러짐 72틱에 대사");

            bw.StepTo(r + 180 + 240);
            Assert.AreEqual(BossFightState.Collapse, bw.Boss.State);
            Assert.IsTrue(bw.P.InputLocked);
            bw.Step(1);
            Assert.AreEqual(BossFightState.CollapseDone, bw.Boss.State, "쓰러짐 240틱");
            Assert.AreEqual(r + 180 + 240, bw.Boss.StateStartTick);
            Assert.IsFalse(bw.P.InputLocked, "CollapseDone 틱에 입력 잠금 해제");

            Assert.AreEqual(hitsBefore, bw.Player.Hits.Count, "되비침·쓰러짐 동안 QueueHit 호출 0");
            Assert.AreEqual(shotsBefore, bw.Threats.Shots.Count, "만개 연출은 판정이 없다");
            Assert.AreEqual(ticketsBefore, bw.Threats.Tickets.Count, "예고 문법도 쓰지 않는다");
            Assert.AreEqual(1, bw.Player.ForceDownTicks.Count);
            Assert.IsTrue(bw.P.AttackLocked, "AttackLocked는 Run의 것이라 건드리지 않는다");
            Assert.AreEqual(1, bw.CountEvents(SimEventType.PhantomReflection));
            Assert.AreEqual(1, bw.CountEvents(SimEventType.PhantomCollapse));
            Assert.AreEqual(1, bw.CountEvents(SimEventType.PhantomMasterLine));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.BossKilled));

            // 그 뒤의 일어남·걷기는 Run이 한다: Dismiss 뒤 w.Boss == null, 몸통 없음.
            bw.Step(20);
            Assert.AreEqual(BossFightState.CollapseDone, bw.Boss.State);
            bw.Sys.Dismiss(bw.W);
            Assert.IsNull(bw.W.Boss);
            bw.Step(1);
            Assert.IsNull(bw.W.FindEnemy(body.Id));
        }

        static int CountBloom(BossWorld bw)
        {
            int n = 0;
            foreach (var e in bw.W.Events)
                if (e.Type == SimEventType.BossPatternStarted && e.Id == BossSystem.ReflectionBloomKey) n++;
            return n;
        }

        static int BloomTick(BossWorld bw, int index)
        {
            foreach (var e in bw.W.Events)
            {
                if (e.Type != SimEventType.BossPatternStarted || e.Id != BossSystem.ReflectionBloomKey) continue;
                if (index-- == 0) return e.Tick;
            }
            return -1;
        }

        // 10.2절: 45초(2700틱, 등장 끝부터) → Reflection.
        [Test]
        public void S06_10_2__ReflectionAfter2700FightTicks()
        {
            EnemyState body;
            int introEnd;
            var bw = FirstEncounter(out body, out introEnd);
            bw.P.Pos = new Vector2(2f, 12f);
            bw.StepTo(introEnd + 2700);
            Assert.AreNotEqual(BossFightState.Reflection, bw.Boss.State);
            Assert.IsFalse(bw.P.InputLocked);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Reflection, bw.Boss.State);
            Assert.AreEqual(introEnd + 2700, bw.TickOf(SimEventType.PhantomReflection));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.PhantomMirrorCrack), "시간으로 끝나면 거울 금이 없다");
            Assert.IsTrue(bw.P.InputLocked);
        }

        // 10.2절 종료 우선순위: 체력 0 > 50% > 45초.
        [Test]
        public void S06_10_2__EndPriority_PlayerDownBeatsFloorBeatsTime()
        {
            // (1) 같은 틱에 플레이어 체력 0과 체력 바닥: Run.OnPlayerDowned → BeginCollapse가 먼저 → 바로 Collapse, 되비침 없음.
            EnemyState body;
            int introEnd;
            var bw = FirstEncounter(out body, out introEnd);
            bw.P.Pos = new Vector2(2f, 12f);
            bw.P.AttackLocked = false;
            bw.Step(40);
            body.Hp = body.HpFloor;
            int t = bw.W.Tick;
            bw.Sys.BeginCollapse(bw.W);
            Assert.AreEqual(BossFightState.Collapse, bw.Boss.State);
            Assert.AreEqual(t, bw.Boss.StateStartTick);
            Assert.IsTrue(bw.P.InputLocked);
            CollectionAssert.AreEqual(new[] { t }, bw.Threats.ClearAllTicks);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Collapse, bw.Boss.State);
            Assert.AreEqual(0, bw.CountEvents(SimEventType.PhantomReflection));
            Assert.AreEqual(0, bw.CountEvents(SimEventType.PhantomMirrorCrack));
            Assert.AreEqual(1, bw.CountEvents(SimEventType.PhantomCollapse));
            bw.Sys.BeginCollapse(bw.W);   // 다시 불려도 시계가 다시 돌지 않는다
            Assert.AreEqual(t, bw.Boss.StateStartTick);
            bw.StepTo(t + 72 + 1);
            Assert.AreEqual(t + 72, bw.TickOf(SimEventType.PhantomMasterLine));
            bw.StepTo(t + 240 + 1);
            Assert.AreEqual(BossFightState.CollapseDone, bw.Boss.State);
            Assert.IsFalse(bw.P.InputLocked);
            Assert.AreEqual(0, bw.Player.ForceDownTicks.Count, "직접 쓰러진 길에서는 ForceDown을 쓰지 않는다");
            Assert.IsFalse(bw.P.AttackLocked);

            // (2) 같은 틱에 체력 바닥과 45초: 바닥이 이긴다(거울 금 연출이 난다). 되비침은 한 번.
            var bw2 = FirstEncounter(out body, out introEnd);
            bw2.P.Pos = new Vector2(2f, 12f);
            bw2.StepTo(introEnd + 2700);
            body.Hp = body.HpFloor;
            bw2.Step(1);
            Assert.AreEqual(1, bw2.CountEvents(SimEventType.PhantomMirrorCrack));
            Assert.AreEqual(1, bw2.CountEvents(SimEventType.PhantomReflection));
            bw2.Step(5);
            Assert.AreEqual(1, bw2.CountEvents(SimEventType.PhantomReflection));

            // (3) 되비침 중에 BeginCollapse가 불려도 무시한다(되비침이 스스로 쓰러뜨린다).
            bw2.Sys.BeginCollapse(bw2.W);
            Assert.AreEqual(BossFightState.Reflection, bw2.Boss.State);
        }

        // 첫 만남이 아닌 보스에서는 BeginCollapse가 아무 일도 하지 않는다.
        [Test]
        public void S06_10_2__BeginCollapseIgnoredOutsideFirstEncounter()
        {
            var bw = BossWorld.Create();
            bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.FinishIntro();
            bw.Sys.BeginCollapse(bw.W);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.IsFalse(bw.P.InputLocked);
        }

        // 웨이브 10이라도 첫 만남 표시가 없으면 가마등이고, 웨이브 20의 허상은 첫 만남이 아니다(체력 7111, 바닥 없음).
        [Test]
        public void S06_10_1__FirstEncounterOnlyAtWave10WithFlag()
        {
            var bw = BossWorld.Create();
            var plan = new WavePlan { Wave = 10, PhantomFirst = true };   // BossId를 비워 두면 SelectBoss로 고른다
            var body = bw.Sys.SpawnBoss(bw.W, plan);
            Assert.AreEqual(Ids.Boss.MirrorPhantom, body.DefId);
            Assert.IsTrue(bw.Boss.FirstEncounter);

            var bw2 = BossWorld.Create();
            var body2 = bw2.Sys.SpawnBoss(bw2.W, new WavePlan { Wave = 10 });
            Assert.AreEqual(Ids.Boss.KilnBoar, body2.DefId);
            Assert.IsFalse(bw2.Boss.FirstEncounter);

            var bw3 = BossWorld.Create();
            var body3 = bw3.Spawn(Ids.Boss.MirrorPhantom, 20, phantomFirst: true);
            Assert.IsFalse(bw3.Boss.FirstEncounter);
            Assert.That(body3.MaxHp, Is.EqualTo(7111f).Within(1f));
            Assert.That(body3.HpFloor, Is.EqualTo(0f));
            Assert.AreEqual(0, bw3.Boss.RageStacks);
            Assert.AreEqual(0, bw3.Boss.Phase);
        }

        // ------------------------------------------------------------ 복사 사격

        // 9.2절 copy_fire: Waiting에서, 플레이어 조립을 적 탄으로(속도 6, 피해 1), 간격 = 플레이어 간격 × 2.5 올림(마력탄 9틱 → 23틱).
        [Test]
        public void S06_9_2__CopyFireShootsPlayerLoadoutAsEnemyShots()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.P.Spell = BossWorld.Spell(TestContent.Db, Ids.Block.MagicBolt, split: 3, fanHalf: 15f);
            int introEnd = bw.FinishIntro();
            bw.Step(60);
            Assert.GreaterOrEqual(bw.Threats.Shots.Count, 6);
            Assert.AreEqual(introEnd + 1, bw.Threats.ShotTicks[0]);
            Assert.AreEqual(introEnd + 1 + 23, bw.Threats.ShotTicks[3], "간격 23틱");
            for (int i = 0; i < bw.Threats.Shots.Count; i++)
            {
                var s = bw.Threats.Shots[i];
                Assert.That(s.Speed, Is.EqualTo(6f));
                Assert.AreEqual(1, s.BaseDamage);
                Assert.IsTrue(s.Grazeable);
                Assert.AreEqual(body.Id, s.OwnerEnemyId);
                Assert.That(s.Radius, Is.EqualTo(0.12f).Within(0.0001f));
            }
            // 3갈래: 가운데가 플레이어 쪽, 좌우 ±19.19도.
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[1].Dir), Is.EqualTo(19.19f).Within(0.01f));
            Assert.That(Vector2.Angle(bw.Threats.Shots[1].Dir, bw.Threats.Shots[2].Dir), Is.EqualTo(19.19f).Within(0.01f));
            Assert.That(Vector2.Angle(bw.Threats.Shots[0].Dir, bw.Threats.Shots[2].Dir), Is.EqualTo(38.38f).Within(0.02f));
            Assert.AreEqual(bw.Threats.Shots[0].CastId, bw.Threats.Shots[2].CastId);
            Assert.AreNotEqual(bw.Threats.Shots[0].CastId, bw.Threats.Shots[3].CastId);
        }

        // 9.2절: 발사 1회 최대 12개. 유효한 주문이 없으면 쏘지 않는다.
        [Test]
        public void S06_9_2__CopyFireCapsAtTwelvePerShot()
        {
            var bw = BossWorld.Create();
            bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.P.Spell = BossWorld.Spell(TestContent.Db, Ids.Block.MagicBolt, split: 3, fanHalf: 15f, copies: 6);   // 18발
            int introEnd = bw.FinishIntro();
            bw.Step(1);
            Assert.AreEqual(12, bw.Threats.Shots.Count);
            Assert.AreEqual(introEnd + 1, bw.Threats.ShotTicks[11]);

            var bw2 = BossWorld.Create();
            bw2.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw2.FinishIntro();
            bw2.Step(200);
            Assert.AreEqual(0, bw2.Threats.Shots.Count);
        }

        // 9.2절: 연쇄 번개 → 0.6초(36틱) 조준 예고선(폭 0.5, 길이 7) 뒤 직선 번개. 화염구는 멈춘 자리에 0.8초 예고 원(반경 1.2).
        [Test]
        public void S06_9_2__ChainBecomesAimLineAndFireballExplodesWithTelegraph()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.P.Spell = BossWorld.Spell(TestContent.Db, Ids.Block.ChainLightning);
            bw.FinishIntro();
            bw.Step(1);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var line = bw.Threats.Tickets[0].Request.Zones[0];
            Assert.AreEqual(ZoneShape.AimLine, line.Shape);
            Assert.AreEqual(36, line.TelegraphTicks);
            Assert.That(line.Width, Is.EqualTo(0.5f));
            Assert.That(line.Length, Is.EqualTo(7f));
            Assert.AreEqual(1, line.BaseDamage);
            Assert.AreEqual(body.Pos, line.Pos);

            var bw2 = BossWorld.Create();
            bw2.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw2.P.Spell = BossWorld.Spell(TestContent.Db, Ids.Block.Fireball);
            bw2.FinishIntro();
            bw2.Step(1);
            Assert.AreEqual(1, bw2.Threats.Shots.Count);
            Assert.AreEqual(0, bw2.Threats.Tickets.Count);
            // 탄이 플레이어 곁에서 사라졌다(맞았다) → 그 자리에 예고 원.
            var shot = bw2.W.EnemyShots[0];
            shot.Pos = bw2.P.Pos;
            shot.Dead = true;
            bw2.Step(2);
            Assert.AreEqual(1, bw2.Threats.Tickets.Count);
            var circle = bw2.Threats.Tickets[0].Request.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, circle.Shape);
            Assert.That(circle.Radius, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.AreEqual(48, circle.TelegraphTicks);
            Assert.AreEqual(1, circle.BaseDamage);
            Assert.AreEqual(bw2.P.Pos, circle.Pos);
        }

        // 9.2절 mimic_art: 2단계부터, 0.8초(48틱) 예고 뒤 따라 쓴다. 해금이 없으면 ①(트리 1회). 빈틈 0.
        [Test]
        public void S06_9_2__MimicArtWarns48TicksThenFires()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.Boss.Phase = 1;                       // 2단계
            bw.P.UnlockedArts.Add(Ids.Art.Burst);    // ③④⑧ 없음 → ① 마력 방출
            int introEnd = bw.FinishIntro();
            int start = introEnd + 90;
            bw.StepTo(start + 1);
            var e = bw.EventOf(SimEventType.BossPatternStarted);
            Assert.AreEqual(start, e.Tick);
            Assert.AreEqual(MirrorPhantomBrain.PatternMimicArt, e.Id);
            Assert.AreEqual(1, e.A, "① = Ids.Art.All의 첫째");
            Assert.AreEqual(48, e.B);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);

            bw.P.Spell = BossWorld.Spell(TestContent.Db, Ids.Block.MagicBolt);   // 예고 중에는 복사 사격이 쉰다
            bw.StepTo(start + 48);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            bw.Step(1);
            Assert.AreEqual(1, bw.Threats.Shots.Count, "① 마력 방출 = 트리 1회");
            Assert.AreEqual(start + 48, bw.Threats.ShotTicks[0]);
            Assert.AreEqual(1, bw.Threats.Shots[0].BaseDamage, "비기 배율과 무관하게 피해 1");
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State, "빈틈 0");
            Assert.AreEqual(MirrorPhantomBrain.PatternMimicArt, bw.Boss.LastPattern);
            Assert.AreEqual(body.Id, bw.Threats.Shots[0].OwnerEnemyId);
        }

        // 9.2절 bloom_mimic: 예고 72틱 → 팔방 전개 0·30·60틱 → 빈틈 120틱.
        [Test]
        public void S06_9_2__BloomMimicThreeVolleysThenRecovery()
        {
            var bw = BossWorld.Create();
            bw.Sys.Brains.Add(Ids.Boss.MirrorPhantom, () => new BloomOnlyBrain());
            bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.Boss.Phase = 2;
            int introEnd = bw.FinishIntro();
            int start = introEnd + 90;
            bw.StepTo(start + 72 + 60 + 1);
            Assert.AreEqual(start, bw.TickOf(SimEventType.BossPatternStarted));
            CollectionAssert.AreEqual(new[] { start + 72, start + 72 + 30, start + 72 + 60 }, BloomOnlyBrain.Of(bw).ActTicks);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(start + 72 + 60 + 120);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        /// <summary>bloom_mimic만 고르고, copy step이 불린 틱을 적는 두뇌(반복·간격·빈틈은 뼈대가 데이터대로 돌린다).</summary>
        sealed class BloomOnlyBrain : BossBrainBase
        {
            public readonly List<int> ActTicks = new List<int>();
            public static BloomOnlyBrain Of(BossWorld bw) => (BloomOnlyBrain)bw.Ctx.Brain;
            public override bool Eligible(BossContext c, BossPattern pattern) => pattern.Key == MirrorPhantomBrain.PatternBloomMimic;
            public override void Move(BossContext c, PlayerState target) { }
            public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
            {
                ActTicks.Add(c.W.Tick);
                return BossActResult.Done;
            }
        }

        // 9.2절 summon_slimes: fightTicks가 1800의 배수가 될 때마다 이끼 슬라임 6(졸개: 보상 없음, SummonerId = 몸통).
        [Test]
        public void S06_9_2__SummonSixSlimesEvery1800FightTicks()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.P.Pos = new Vector2(2f, 12f);
            int introEnd = bw.FinishIntro();
            bw.StepTo(introEnd + 1800);
            Assert.AreEqual(0, bw.Enemies.Spawned.Count);
            bw.Step(1);
            Assert.AreEqual(6, bw.Enemies.Spawned.Count);
            foreach (var s in bw.Enemies.Spawned)
            {
                Assert.AreEqual(Ids.Enemy.SlimeMoss, s.DefId);
                Assert.IsTrue(s.NoReward);
                Assert.AreEqual(body.Id, s.SummonerId);
                Assert.IsTrue(bw.W.InArena(s.Pos));
            }
            Assert.AreEqual(introEnd + 1800, bw.Enemies.SpawnTicks[0]);
            Assert.AreEqual(6, bw.CountEvents(SimEventType.BossMinionSpawned));
            Assert.AreEqual(6, bw.Ctx.CountSummoned(Ids.Enemy.SlimeMoss));
        }

        // 9.2절 이동: 플레이어와 거리 5 유지, 벽에서 1.5 안쪽.
        [Test]
        public void S06_9_2__PhantomKeepsDistanceFiveInsideWallMargin()
        {
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 20);
            bw.P.Pos = new Vector2(12f, 12.5f);   // 허상은 (12, 3.5)에서 나온다 → 거리 9
            bw.FinishIntro();
            bw.Step(240);
            Assert.That((body.Pos - bw.P.Pos).magnitude, Is.EqualTo(5f).Within(0.3f));

            // 플레이어가 다가오면 물러나되 벽에서 1.5칸 안쪽에 머문다.
            bw.P.Pos = new Vector2(12f, 5f);
            bw.Step(400);
            Assert.GreaterOrEqual(body.Pos.y, 1.5f - 0.0001f);
            Assert.LessOrEqual(body.Pos.y, bw.W.ArenaHeight - 1.5f + 0.0001f);
            Assert.GreaterOrEqual(body.Pos.x, 1.5f - 0.0001f);
            Assert.LessOrEqual(body.Pos.x, bw.W.ArenaWidth - 1.5f + 0.0001f);
        }
    }
}
