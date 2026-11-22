using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>밤삼킴(05 요약서 9.6절). Threats·Enemies·Player는 가짜(G07의 BossWorld).</summary>
    public class NightSerpentTests
    {
        const string Id = Ids.Boss.NightSerpent;

        static NightSerpentBrain Brain(BossWorld bw) => (NightSerpentBrain)((ForcedBrain)bw.Ctx.Brain).Inner;

        // 9.6절: 마디 10개 + 꼬리(BossState.Segments 11개, 마지막이 꼬리)가 머리가 지나간 길을 간격 1.0으로 따라온다.
        [Test]
        public void S06_9_6__SegmentsFollowHeadPath()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern, still: false);
            Assert.AreEqual(11, bw.Boss.Segments.Count);
            Assert.That(bw.Body.BodyRadius, Is.EqualTo(0.75f), "몸통 원 = 머리");
            bw.P.Pos = new Vector2(23f, 3.5f);
            bw.FinishIntro();
            bw.Step(150);
            bw.P.Pos = new Vector2(1f, 11f);
            bw.Step(250);
            // 머리가 12칸 넘게 움직였으니 몸 전체가 길 위에 펴져 있다: 이웃한 부위 사이는 1.0(길이 꺾인 곳은 조금 짧다).
            var seg = bw.Boss.Segments;
            Assert.AreEqual(11, seg.Count);
            Vector2 prev = bw.Body.Pos;
            for (int i = 0; i < seg.Count; i++)
            {
                float d = (seg[i] - prev).magnitude;
                Assert.That(d, Is.InRange(0.6f, 1.0f + 0.01f), "부위 " + i);
                Assert.IsTrue(bw.W.InArena(seg[i]));
                prev = seg[i];
            }
            // 속도 3.5칸/초.
            Vector2 before = bw.Body.Pos;
            bw.Step(1);
            Assert.That((bw.Body.Pos - before).magnitude, Is.EqualTo(3.5f / 60f).Within(0.0005f));
        }

        // 계약: 머리·마디·꼬리 = 같은 적. 마디 여럿이 같은 틱에 닿아도 QueueHit은 한 번이고 AttackId는 몸통 Id 하나, 쿨다운 90틱.
        [Test]
        public void S06_9_6__ManyPartsTouchingQueueOneHitWithBodyId()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern);
            var body = bw.Body;
            bw.FinishIntro();
            // 등장 자리(12, 3.5)에서 아래로 편 몸은 벽에 막혀 뒤쪽 부위들이 한 자리에 모여 있다.
            var seg = bw.Boss.Segments;
            Vector2 pile = seg[seg.Count - 1];
            int touching = 0;
            for (int i = 0; i < seg.Count; i++) if ((seg[i] - pile).magnitude < 0.5f + 0.15f) touching++;
            Assert.GreaterOrEqual(touching, 3, "마디 셋 이상이 같은 자리에 겹쳐 있어야 이 테스트가 뜻이 있다");
            Assert.Greater((pile - body.Pos).magnitude, 0.75f + 0.5f, "머리에서는 떨어져 있다");

            bw.P.Pos = pile;
            int tick = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(1, bw.Player.Hits.Count);
            var hit = bw.Player.Hits[0];
            Assert.AreEqual(tick, bw.Player.HitTicks[0]);
            Assert.AreEqual(PlayerDamageSource.Contact, hit.Source);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(body.Id, hit.SourceEnemyId);
            Assert.AreEqual(1, hit.DamageCells);
            bw.StepTo(tick + 90);
            Assert.AreEqual(1, bw.Player.Hits.Count, "같은 적 접촉은 1.5초에 1회");
            bw.Step(1);
            Assert.AreEqual(2, bw.Player.Hits.Count);
            Assert.AreEqual(body.Id, bw.Player.Hits[1].AttackId);
        }

        // 접촉 쿨다운은 QueueHit이 true일 때만 소비한다.
        [Test]
        public void S06_9_6__ContactCooldownOnlyWhenQueued()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern);
            bw.FinishIntro();
            var seg = bw.Boss.Segments;
            bw.P.Pos = seg[seg.Count - 1];
            bw.Player.QueueResult = false;
            bw.Step(5);
            Assert.AreEqual(5, bw.Player.Hits.Count, "무적 등으로 들어가지 않으면 매 틱 다시 올린다");
            bw.Player.QueueResult = true;
            bw.Step(5);
            Assert.AreEqual(6, bw.Player.Hits.Count);
        }

        // 9.6절: 받는 피해 머리 ×1.0, 마디·꼬리 ×0.5(ModifyIncomingDamage 경로).
        [Test]
        public void S06_9_6__DamageRatioByPart()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern);
            var body = bw.Body;
            bw.FinishIntro();
            var seg = bw.Boss.Segments;
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, body.Pos, body.Pos), Is.EqualTo(10f));
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, body.Pos + new Vector2(0.5f, 0.5f), body.Pos + new Vector2(0.5f, 0.5f)), Is.EqualTo(10f));
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, seg[1], seg[1]), Is.EqualTo(5f));
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, seg[seg.Count - 1], seg[seg.Count - 1]), Is.EqualTo(5f));
            Assert.That(bw.Sys.ModifyIncomingDamage(bw.W, body, 10f, false, new Vector2(22f, 12f), new Vector2(22f, 12f)), Is.EqualTo(10f), "어느 부위에도 닿지 않은 자리 = 몸통(머리)");
        }

        // 9.6절 coil + 계약: 플레이어 둘레 ring(바깥 5.0 / 안 4.0), 72틱 → 몸으로 닫힌 고리 240틱(접촉 피해 1, 같은 적) →
        // 가둠이 끝난 틱에 무는 띠(폭 1.5, 48틱: 머리 → 플레이어 → 고리 반대편) → 돌진 시작 틱 = 요청 틱 + 48, Dash·AttackId = 몸통 Id·피해 2 → 빈틈 90틱.
        [Test]
        public void S06_9_6__CoilThenBite()
        {
            var bw = FourKit.Make(Id, 60, NightSerpentBrain.PatternCoil);
            var body = bw.Body;
            Vector2 center = new Vector2(12f, 7f);
            bw.P.Pos = center;
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, NightSerpentBrain.PatternCoil);
            var ring = bw.Threats.Tickets[0].Request.Zones[0];
            Assert.AreEqual(1, bw.Threats.Tickets[0].Request.Zones.Count);
            Assert.AreEqual(ZoneShape.Ring, ring.Shape);
            Assert.That(ring.Radius, Is.EqualTo(5f));
            Assert.That(ring.InnerRadius, Is.EqualTo(4f));
            Assert.AreEqual(72, ring.TelegraphTicks);
            Assert.AreEqual(1, ring.BaseDamage);
            Assert.AreEqual(PlayerDamageSource.Lingering, ring.DamageSource);
            Assert.AreEqual(center, ring.Pos);

            var brain = Brain(bw);
            bw.StepTo(r + 72);
            Assert.IsFalse(brain.CoilActive);
            bw.Step(1);   // 고리 예고의 판정 틱: 몸이 고리를 닫는다
            Assert.IsTrue(brain.CoilActive);
            Assert.AreEqual(r + 72 + 240, brain.CoilUntilTick);
            Assert.That(brain.CoilMidRadius, Is.EqualTo(4.5f));
            Assert.That((body.Pos - center).magnitude, Is.EqualTo(4.5f).Within(0.001f));
            foreach (var s in bw.Boss.Segments) Assert.That((s - center).magnitude, Is.EqualTo(4.5f).Within(0.001f));

            // 고리 안(가운데)에 있으면 닿지 않는다.
            bw.Step(30);
            Assert.AreEqual(0, bw.Player.Hits.Count);
            // 고리 띠(안 4.0 ~ 바깥 5.0) 위는 부위 사이의 틈이어도 몸이다: 접촉 피해 1, 같은 적(AttackId = 몸통 Id).
            bw.P.Pos = center + new Vector2(0f, -4.5f);
            int tick = bw.W.Tick;
            bw.Step(1);
            Assert.AreEqual(1, bw.Player.Hits.Count);
            Assert.AreEqual(PlayerDamageSource.Contact, bw.Player.Hits[0].Source);
            Assert.AreEqual(body.Id, bw.Player.Hits[0].AttackId);
            Assert.AreEqual(1, bw.Player.Hits[0].DamageCells);
            bw.Step(30);
            Assert.AreEqual(1, bw.Player.Hits.Count, "같은 적 쿨다운 하나");
            Assert.Less(tick + 31, r + 72 + 240);
            bw.P.Pos = center;

            // 가둠이 끝난 틱에 무는 띠 예고.
            int coilEnd = r + 72 + 240;
            bw.StepTo(coilEnd);
            Assert.IsTrue(brain.CoilActive);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            Vector2 head = body.Pos;
            bw.Step(1);
            Assert.IsFalse(brain.CoilActive);
            Assert.AreEqual(2, bw.Threats.Tickets.Count);
            var bite = bw.Threats.Tickets[1];
            Assert.AreEqual(coilEnd, bite.RequestTick);
            var band = bite.Request.Zones[0];
            Assert.AreEqual(ZoneShape.Band, band.Shape);
            Assert.That(band.Width, Is.EqualTo(1.5f));
            Assert.AreEqual(48, band.TelegraphTicks);
            Assert.AreEqual(0, band.BaseDamage);
            Assert.AreEqual(PlayerDamageSource.Dash, band.DamageSource);
            Assert.That((band.Pos - body.Pos).magnitude, Is.LessThan(0.001f), "머리 위치에서");
            Assert.That(Vector2.Angle(band.Dir, center - band.Pos), Is.LessThan(0.01f), "그 틱의 플레이어 위치를 지나");
            Assert.That(band.Length, Is.EqualTo(9f).Within(0.01f), "고리 반대편까지(지름 9.0)");
            head = body.Pos;

            bw.StepTo(coilEnd + 48);
            Assert.IsFalse(body.DashActive);
            Assert.AreEqual(0, FourKit.CountHits(bw, PlayerDamageSource.Dash));
            bw.Step(1);   // 판정 틱: 이 틱부터 문다
            Assert.IsTrue(body.DashActive);
            Assert.AreEqual(2, body.DashBaseDamage);
            Assert.That((body.Pos - head).magnitude, Is.EqualTo(16f / 60f).Within(0.001f));
            int end = FourKit.RunDash(bw);
            Assert.AreEqual(1, FourKit.CountHits(bw, PlayerDamageSource.Dash));
            var hit = FourKit.FirstHit(bw, PlayerDamageSource.Dash);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(2, hit.DamageCells);
            // 고리 반대편에서 멈춘다(16칸/초라 한 틱 0.27칸까지 넘을 수 있다).
            Assert.That((body.Pos - head).magnitude, Is.InRange(9f - 0.001f, 9f + 16f / 60f));
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(end + 90);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 9.6절 dark_spit: aim_line 폭 0.4, 42틱 중 마지막 24틱 고정 → 같은 고정 방향으로 3발(2회차 5발), 30틱 간격, 속도 9, 소 0.12. 빈틈 36틱.
        [TestCase(60, 3)]
        [TestCase(120, 5)]
        public void S06_9_6__DarkSpitSameLockedDirection(int wave, int shots)
        {
            var bw = FourKit.Make(Id, wave, NightSerpentBrain.PatternSpit);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, NightSerpentBrain.PatternSpit);
            var z = bw.Threats.Tickets[0].Request.Zones[0];
            Assert.AreEqual(ZoneShape.AimLine, z.Shape);
            Assert.That(z.Width, Is.EqualTo(0.4f));
            Assert.AreEqual(42, z.TelegraphTicks);
            Assert.AreEqual(24, z.LockLastTicks);
            Assert.IsTrue(z.AimAtPlayer);
            bw.StepTo(r + 42);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            for (int k = 0; k < shots; k++)
            {
                FourKit.RunThrough(bw, r + 42 + 30 * k);
                Assert.AreEqual(k + 1, bw.Threats.Shots.Count);
                Assert.AreEqual(r + 42 + 30 * k, bw.Threats.ShotTicks[k]);
                // 첫 발 뒤 플레이어가 움직여도 방향은 고정된 그대로다.
                bw.P.Pos = new Vector2(3f + k, 12f);
            }
            var s = bw.Threats.Shots;
            Assert.That(s[0].Speed, Is.EqualTo(9f));
            Assert.That(s[0].Radius, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.AreEqual(1, s[0].BaseDamage);
            for (int k = 1; k < shots; k++) Assert.That(Vector2.Angle(s[0].Dir, s[k].Dir), Is.LessThan(0.01f));
            Assert.AreEqual(1, bw.Threats.Tickets.Count, "조준선은 한 번");
            int end = r + 42 + 30 * (shots - 1);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(end + 36);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 9.6절 burrow: 잠행(땅속: 피해·효과·접촉 없음) 뒤 플레이어 위치에 3번 솟는다. circle r2.0, 각 54틱, 피해 2.
        // 각 원 = 그 원 예고 시작 틱의 플레이어 위치, 다음 예고 = 앞 원이 솟는 틱. 세 번째에 땅 위로(빈틈 120틱, 이때부터 맞는다).
        [Test]
        public void S06_9_6__BurrowRisesThreeTimes()
        {
            var bw = FourKit.Make(Id, 60, NightSerpentBrain.PatternBurrow);
            var body = bw.Body;
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, NightSerpentBrain.PatternBurrow);
            Assert.IsTrue(body.Burrowed, "첫 원의 예고는 잠행 시작 틱");
            Assert.IsTrue(body.Has(EnemyTags.Burrowed));
            Assert.IsFalse(body.Targetable);
            var spots = new[] { bw.P.Pos, new Vector2(4f, 4f), new Vector2(20f, 5f) };
            for (int k = 0; k < 3; k++)
            {
                if (k > 0)
                {
                    bw.P.Pos = spots[k];
                    bw.StepTo(r + 54 * k);
                    Assert.AreEqual(k, bw.Threats.Tickets.Count);
                    Assert.IsTrue(body.Burrowed);
                    bw.Step(1);   // 앞 원이 솟는 틱 = 다음 원의 예고 시작
                    Assert.That((body.Pos - spots[k - 1]).magnitude, Is.LessThan(0.001f), "솟은 자리로 옮긴다");
                    Assert.IsTrue(body.Burrowed, "세 번째 전에는 땅속 그대로");
                }
                Assert.AreEqual(k + 1, bw.Threats.Tickets.Count);
                var t = bw.Threats.Tickets[k];
                Assert.AreEqual(r + 54 * k, t.RequestTick);
                var z = t.Request.Zones[0];
                Assert.AreEqual(ZoneShape.Circle, z.Shape);
                Assert.That(z.Radius, Is.EqualTo(2f));
                Assert.AreEqual(54, z.TelegraphTicks);
                Assert.AreEqual(2, z.BaseDamage);
                Assert.AreEqual(PlayerDamageSource.Telegraph, z.DamageSource);
                Assert.AreEqual(spots[k], z.Pos);
            }
            bw.P.Pos = new Vector2(12f, 12f);
            bw.StepTo(r + 162);
            Assert.IsTrue(body.Burrowed);
            Assert.AreEqual(0, bw.Player.Hits.Count, "땅속에서는 접촉이 없다");
            bw.Step(1);   // 세 번째 원이 솟는 틱
            Assert.IsFalse(body.Burrowed);
            Assert.IsFalse(body.Has(EnemyTags.Burrowed));
            Assert.IsTrue(body.Targetable, "이때부터 맞는다");
            Assert.That((body.Pos - spots[2]).magnitude, Is.LessThan(0.001f));
            Assert.AreEqual(3, bw.Threats.Tickets.Count);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(r + 162 + 120);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            Assert.That((body.Pos - spots[2]).magnitude, Is.LessThan(0.001f), "머리가 2.0초 땅 위에 남는다");
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        // 단계 전환이 잠행을 끊어도 땅속에 남지 않는다.
        [Test]
        public void S06_9_6__AbortedBurrowSurfaces()
        {
            var bw = FourKit.Make(Id, 60, NightSerpentBrain.PatternBurrow);
            bw.FinishIntro();
            FourKit.WaitPattern(bw, NightSerpentBrain.PatternBurrow);
            Assert.IsTrue(bw.Body.Burrowed);
            FourKit.ToPhase(bw, 1);
            Assert.AreEqual(BossFightState.PhaseTransition, bw.Boss.State);
            Assert.IsFalse(bw.Body.Burrowed);
            Assert.IsFalse(bw.Body.Has(EnemyTags.Burrowed));
        }

        // 9.6절 scale_volley(2단계 상시): 움직이는 동안 90틱마다 마디가 36틱 깜박인 뒤 마디 10개가 바깥쪽(전장 중심에서 먼 쪽 법선)으로 1발씩(속도 4, 소 0.12).
        [Test]
        public void S06_9_6__ScaleVolleyWhileMoving()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern, still: false);
            var body = bw.Body;
            bw.P.Pos = new Vector2(23f, 3.5f);
            bw.FinishIntro();
            bw.Step(200);
            Assert.AreEqual(0, FourKit.CountPattern(bw, NightSerpentBrain.PatternVolley), "1단계에는 비늘 탄이 없다");
            bw.P.Pos = new Vector2(1f, 11f);
            FourKit.ToPhase(bw, 1);
            int v = FourKit.WaitPattern(bw, NightSerpentBrain.PatternVolley);
            Assert.GreaterOrEqual(v, 0);
            Assert.AreEqual(36, FourKit.PatternEvent(bw, NightSerpentBrain.PatternVolley, 0).B);
            Assert.IsTrue(body.WeaponTint, "마디 깜박임");
            Assert.AreEqual(NightSerpentBrain.PatternVolley, bw.Boss.CurrentPattern);
            bw.StepTo(v + 36);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            // 발사 직전의 마디 자리(발사 틱의 이동이 먼저 반영되므로 넉넉히 본다).
            bw.Step(1);
            Assert.AreEqual(10, bw.Threats.Shots.Count);
            Assert.IsNull(bw.Boss.CurrentPattern);
            Assert.IsFalse(body.WeaponTint);
            Vector2 arenaCenter = new Vector2(bw.W.ArenaWidth / 2f, bw.W.ArenaHeight / 2f);
            var seg = bw.Boss.Segments;
            for (int i = 0; i < 10; i++)
            {
                var s = bw.Threats.Shots[i];
                Assert.AreEqual(v + 36, bw.Threats.ShotTicks[i]);
                Assert.That(s.Speed, Is.EqualTo(4f));
                Assert.That(s.Radius, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.AreEqual(1, s.BaseDamage);
                Assert.AreEqual(bw.Threats.Shots[0].CastId, s.CastId);
                Assert.That((s.Pos - seg[i]).magnitude, Is.LessThan(0.001f), "마디 " + i + "에서 나간다");
                Assert.GreaterOrEqual(Vector2.Dot(s.Dir, s.Pos - arenaCenter), -0.001f, "전장 중심에서 먼 쪽");
                Vector2 along = (i == 0 ? body.Pos : seg[i - 1]) - seg[i + 1];
                Assert.That(Mathf.Abs(Vector2.Dot(s.Dir, along.normalized)), Is.LessThan(0.001f), "진행 방향의 법선");
            }
            // 다음 깜박임은 앞 깜박임 + 90틱.
            int v2 = FourKit.WaitPattern(bw, NightSerpentBrain.PatternVolley);
            Assert.AreEqual(v + 90, v2);
        }

        // 9.6절: 비늘 탄은 보스가 움직일 때만.
        [Test]
        public void S06_9_6__NoScaleVolleyWhileStill()
        {
            var bw = FourKit.Make(Id, 60, FourKit.NoPattern, still: true);
            bw.FinishIntro();
            FourKit.ToPhase(bw, 1);
            bw.Step(600);
            Assert.AreEqual(0, FourKit.CountPattern(bw, NightSerpentBrain.PatternVolley));
            Assert.AreEqual(0, bw.Threats.Shots.Count);
        }

        // 공통 규칙 3: 대상 플레이어가 없으면 그 틱은 가만히.
        [Test]
        public void S06_Rule3__NoTargetNoAction()
        {
            var bw = FourKit.Make(Id, 60, null, still: false);
            bw.FinishIntro();
            FourKit.ToPhase(bw, 1);
            bw.P.Downed = true;
            Vector2 before = bw.Body.Pos;
            bw.Step(400);
            Assert.AreEqual(before, bw.Body.Pos);
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
        }
    }
}
