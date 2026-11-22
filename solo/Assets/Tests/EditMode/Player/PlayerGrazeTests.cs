using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>스쳐 피하기: 띠 판정(후보를 직접 넣는다)과 보상. 02 요약서 7절.</summary>
    public class PlayerGrazeTests
    {
        const int Hu = 120;

        /// <summary>냉각이 섞이지 않게 "방금 쐈다"로 둔다(냉각 대기 중). 발사 가능 틱은 건드리지 않는다.</summary>
        static void HoldCooling(Kit k)
        {
            k.P.LastFireTick = k.W.Tick;
            k.P.LastFireIntervalTicks = 100000;
        }

        static int GrazeOnce(Kit k, GrazeCandidate c)
        {
            int before = k.Count(SimEventType.GrazeAwarded);
            k.Threats.Candidates.Add(c);
            k.Step();
            return k.Count(SimEventType.GrazeAwarded) - before;
        }

        // T-G12: 탄 반지름 0.12, 거리 0.70 → 스침(0.70 ≤ 0.72). T-G13: 0.73 → 없음.
        // T-G14: 초롱 0.85 → 스침(≤ 0.87). T-G15: 0.26 → 피격 거리(≤ 0.27), 스침 아님.
        [TestCase("T-G12", null, 0.70f, true)]
        [TestCase("T-G13", null, 0.73f, false)]
        [TestCase("T-G14", "char.2", 0.85f, true)]
        [TestCase("T-G14b", null, 0.85f, false)]
        [TestCase("T-G15", null, 0.26f, false)]
        public void S01_T_G12_G15__BulletBand(string vector, string charId, float distance, bool graze)
        {
            const float bulletRadius = 0.12f;
            var k = Kit.Create(charId);
            int n = GrazeOnce(k, Kit.Bullet(1, distance - bulletRadius));
            Assert.AreEqual(graze ? 1 : 0, n);
            Assert.AreEqual(graze, k.P.GrazedCasts.Contains(1));
        }

        // T-G16: 구역 원 반지름 1.0, 중심 거리 1.5(경계 바깥 0.5), 그 틱에 폭발 → 스침. T-G17: 1.7(바깥 0.7) → 없음.
        // T-G18: 초롱, 바깥 0.7 → 스침(감지 0.75, D-09).
        [TestCase("T-G16", null, 1.5f, true)]
        [TestCase("T-G17", null, 1.7f, false)]
        [TestCase("T-G18", "char.2", 1.7f, true)]
        public void S01_T_G16_G18__ZoneBandAtDetonation(string vector, string charId, float centerDistance, bool graze)
        {
            const float zoneRadius = 1.0f;
            var k = Kit.Create(charId);
            var c = new GrazeCandidate { CastId = 4, Kind = GrazeKind.Zone, SurfaceDistance = centerDistance - zoneRadius, ThreatId = 2 };
            Assert.AreEqual(graze ? 1 : 0, GrazeOnce(k, c));
            if (graze)
            {
                SimEvent e;
                Assert.IsTrue(k.TryLast(SimEventType.GrazeAwarded, out e));
                Assert.AreEqual((int)GrazeKind.Zone, e.B);
                Assert.AreEqual(4, e.A);
            }
        }

        // T-G19: 폭발 전 틱에는 경계 바깥 0.3이어도 스침 없음(터지는 순간만 = 후보가 그 틱에만 온다).
        [Test]
        public void S01_T_G19__NoZoneGrazeBeforeDetonation()
        {
            var k = Kit.Create();
            k.Step(5);
            Assert.AreEqual(0, k.Count(SimEventType.GrazeAwarded));
            var c = new GrazeCandidate { CastId = 4, Kind = GrazeKind.Zone, SurfaceDistance = 0.3f, ThreatId = 2 };
            Assert.AreEqual(1, GrazeOnce(k, c));
        }

        // 7.1절 띠의 경계: hit_radius < 겉면 거리 ≤ graze_radius.
        [TestCase(0.6f, true)]
        [TestCase(0.15f, false)]
        [TestCase(0.1501f, true)]
        [TestCase(0.6001f, false)]
        public void S01_8_1__BandBoundaries(float surface, bool graze)
        {
            var k = Kit.Create();
            Assert.AreEqual(graze ? 1 : 0, GrazeOnce(k, Kit.Bullet(1, surface)));
        }

        // T-G01: 열 40에서 스침 → 열 85, 열 면제 대기, 가속 90틱, 콤보 +2.
        [Test]
        public void S01_T_G01__Rewards()
        {
            var k = Kit.Create();
            k.StepTo(10);
            k.P.HeatHu = 40 * Hu;
            HoldCooling(k);
            Assert.AreEqual(1, GrazeOnce(k, Kit.Bullet(1, 0.4f)));
            Assert.AreEqual(85 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.P.HeatExemptArmed);
            Assert.AreEqual(10 + 90, k.P.GrazeBuffUntilTick);
            Assert.AreEqual(2, k.P.Combo);
            Assert.AreEqual(1, k.Count(SimEventType.HeatExemptArmed));
            Assert.AreEqual(1, k.Count(SimEventType.GrazeBuffStarted));
            Assert.AreEqual(1, k.Traits.GrazeCalls);
            Assert.AreEqual(1, k.Run.GrazeCalls);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboChanged, out e));
            Assert.AreEqual(2, e.A);
            Assert.AreEqual(2, e.B);
        }

        // T-G02: T-G01 직후 마력탄 1발 → 열 85 그대로, 임계. T-G03: 1발 더 → 열 89, 임계.
        [Test]
        public void S01_T_G02_G03__ExemptShotIsCritThenNextHeats()
        {
            var k = Kit.Create();
            k.P.HeatHu = 40 * Hu;
            HoldCooling(k);
            GrazeOnce(k, Kit.Bullet(1, 0.4f));
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(1, k.Spells.Shots.Count);
            Assert.AreEqual(85 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.Spells.Shots[0].IsCrit);
            Assert.IsTrue(k.Spells.Shots[0].HeatExempt);
            Assert.IsFalse(k.P.HeatExemptArmed);
            k.Step(9);
            Assert.AreEqual(2, k.Spells.Shots.Count);
            Assert.AreEqual(89 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.Spells.Shots[1].IsCrit);
            Assert.IsFalse(k.Spells.Shots[1].HeatExempt);
        }

        // T-G04: 열 92에서 스침 → 열 92 그대로, 면제 대기.
        [Test]
        public void S01_T_G04__HeatNotLoweredWhenAboveCrit()
        {
            var k = Kit.Create();
            k.P.HeatHu = 92 * Hu;
            HoldCooling(k);
            GrazeOnce(k, Kit.Bullet(1, 0.4f));
            Assert.AreEqual(92 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.P.HeatExemptArmed);
        }

        // T-G05: 잠금 중 스침 → 잠금 해제, 열 = 임계 시작(85), 면제 대기, LockBrokenByGraze. T-G06: 달구 → 열 80.
        [TestCase(null, 85)]
        [TestCase("char.1", 80)]
        [TestCase("char.4", 90)]
        public void S01_T_G05_G06__GrazeBreaksLock(string charId, int heat)
        {
            var k = Kit.Create(charId);
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.P.UnlockTick = 900;
            HoldCooling(k);
            GrazeOnce(k, Kit.Bullet(1, 0.4f));
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(heat * Hu, k.P.HeatHu);
            Assert.IsTrue(k.P.HeatExemptArmed);
            Assert.AreEqual(1, k.Count(SimEventType.LockBrokenByGraze));
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatUnlocked, out e));
            Assert.AreEqual((int)HeatUnlockReason.Graze, e.A);
        }

        // T-G07: 스침 2회 연속(다른 시전) 뒤 발사 2발 → 첫 발만 면제, 둘째 발은 열이 오른다.
        [Test]
        public void S01_T_G07__ExemptDoesNotStack()
        {
            var k = Kit.Create();
            k.P.HeatHu = 90 * Hu;
            HoldCooling(k);
            GrazeOnce(k, Kit.Bullet(1, 0.4f));
            GrazeOnce(k, Kit.Bullet(2, 0.4f));
            Assert.AreEqual(1, k.Count(SimEventType.HeatExemptArmed), "면제는 참/거짓이라 겹치지 않는다");
            k.Press(LogicalKey.Fire);
            k.Step(10);
            Assert.AreEqual(2, k.Spells.Shots.Count);
            Assert.IsTrue(k.Spells.Shots[0].HeatExempt);
            Assert.IsFalse(k.Spells.Shots[1].HeatExempt);
            Assert.AreEqual(94 * Hu, k.P.HeatHu);
        }

        // T-G08: 가속 남은 시간 30틱에서 스침 → 가속 기한 = 지금 + 90(갱신, 중첩 없음). T-G09: 초롱 120틱.
        [TestCase(null, 90)]
        [TestCase("char.2", 120)]
        public void S01_T_G08_G09__BuffRefreshes(string charId, int ticks)
        {
            var k = Kit.Create(charId);
            k.StepTo(50);
            k.P.GrazeBuffUntilTick = 80;
            GrazeOnce(k, Kit.Bullet(1, 0.4f));
            Assert.AreEqual(50 + ticks, k.P.GrazeBuffUntilTick);
            k.StepTo(50 + ticks - 1);
            Assert.AreEqual(0, k.Count(SimEventType.GrazeBuffEnded));
            k.Step();
            Assert.AreEqual(1, k.Count(SimEventType.GrazeBuffEnded));
        }

        // T-G10: 한 시전의 탄 3개가 차례로 감지 원을 지남 → 스침 1회만.
        [Test]
        public void S01_T_G10__SameCastOnlyOnce()
        {
            var k = Kit.Create();
            Assert.AreEqual(1, GrazeOnce(k, Kit.Bullet(7, 0.5f, 11)));
            Assert.AreEqual(0, GrazeOnce(k, Kit.Bullet(7, 0.5f, 12)));
            Assert.AreEqual(0, GrazeOnce(k, Kit.Bullet(7, 0.3f, 13)));
            Assert.AreEqual(2, k.P.Combo);
        }

        // T-G11: 시전 A의 탄과 시전 B의 탄이 같은 틱에 감지 → 스침 2회, 콤보 +4. 처리는 위협 id 오름차순.
        [Test]
        public void S01_T_G11__TwoCastsSameTickBothAwardedInIdOrder()
        {
            var k = Kit.Create();
            k.Threats.Candidates.Add(Kit.Bullet(20, 0.5f, 9));
            k.Threats.Candidates.Add(Kit.Bullet(10, 0.5f, 3));
            k.Step();
            Assert.AreEqual(2, k.Count(SimEventType.GrazeAwarded));
            Assert.AreEqual(4, k.P.Combo);
            var order = new List<int>();
            foreach (var e in k.W.Events) if (e.Type == SimEventType.GrazeAwarded) order.Add(e.A);
            CollectionAssert.AreEqual(new[] { 10, 20 }, order);
        }

        // 계약: 이번 틱에 피격 후보가 올라왔으면(HasPendingHit) 그 틱의 스침은 전부 버린다.
        [Test]
        public void Contract__PendingHitDiscardsGrazeThisTick()
        {
            var k = Kit.Create();
            k.HitOnTick(0, 1, 1);
            k.Threats.Candidates.Add(Kit.Bullet(5, 0.5f));
            k.Threats.Candidates.Add(Kit.Bullet(6, 0.5f));
            k.Step();
            Assert.AreEqual(0, k.Count(SimEventType.GrazeAwarded));
            Assert.AreEqual(0, k.P.GrazedCasts.Count);
            Assert.IsFalse(k.P.HeatExemptArmed);
            Assert.AreEqual(500, k.P.Hp);
        }

        // 7.1절: 피격 후 무적 중에는 인정하지 않는다.
        [Test]
        public void S01_8_1__NoGrazeWhileInvulnerable()
        {
            var k = Kit.Create();
            k.HitOnTick(0, 1, 1);
            k.Step();
            Assert.AreEqual(0, GrazeOnce(k, Kit.Bullet(5, 0.5f)));
            k.StepTo(60);
            Assert.AreEqual(1, GrazeOnce(k, Kit.Bullet(5, 0.5f)), "무적이 끝난 틱(60)부터 다시 인정");
        }

        // 잔상 걸음: 무적 중 스침 창은 1회만 허용한다.
        [Test]
        public void S04_Afterimage__ArtGrazeWindowAllowsExactlyOne()
        {
            var k = Kit.Create();
            k.Sys.GrantInvuln(k.W, k.P, 9, true);
            k.Threats.Candidates.Add(Kit.Bullet(5, 0.5f, 1));
            k.Threats.Candidates.Add(Kit.Bullet(6, 0.5f, 2));
            k.Step();
            Assert.AreEqual(1, k.Count(SimEventType.GrazeAwarded));
            Assert.IsTrue(k.P.ArtGrazeWindowUsed);
            Assert.AreEqual(0, GrazeOnce(k, Kit.Bullet(7, 0.5f, 3)), "창을 이미 썼다");

            // 창 없는 비기 무적(벼락 질주)은 인정하지 않는다.
            var b = Kit.Create();
            b.Sys.GrantInvuln(b.W, b.P, 12, false);
            Assert.AreEqual(0, GrazeOnce(b, Kit.Bullet(5, 0.5f)));
        }

        // Q-13: 쓰러진 뒤에는 스침을 인정하지 않는다(보상·이벤트 없음).
        [Test]
        public void OQ_Q13__NoGrazeAfterDowned()
        {
            var k = Kit.Create();
            k.Sys.ForceDown(k.W, k.P, PlayerDamageSource.Scripted);
            k.W.Events.Clear();
            Assert.AreEqual(0, GrazeOnce(k, Kit.Bullet(5, 0.5f)));
            k.Sys.AwardGraze(k.W, k.P, 8, GrazeKind.Bullet, Vector2.zero);
            Assert.AreEqual(0, k.Count(SimEventType.GrazeAwarded));
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(0, k.Run.GrazeCalls);
        }

        // 벼락 질주의 길 위 탄: Arts가 AwardGraze를 직접 부른다(무적 중이어도 보상이 들어간다).
        [Test]
        public void S04_BoltDash__DirectAwardWorksDuringInvuln()
        {
            var k = Kit.Create("char.2");
            k.Sys.GrantInvuln(k.W, k.P, 12, false);
            k.Sys.AwardGraze(k.W, k.P, 31, GrazeKind.BoltDash, new Vector2(3f, 4f));
            Assert.AreEqual(2, k.P.Combo);
            Assert.IsTrue(k.P.HeatExemptArmed);
            Assert.AreEqual(120, k.P.GrazeBuffUntilTick);
            Assert.AreEqual(1, k.Traits.GrazeCalls);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.GrazeAwarded, out e));
            Assert.AreEqual((int)GrazeKind.BoltDash, e.B);
            Assert.AreEqual(new Vector2(3f, 4f), e.Pos);
        }

        // 7.1절 [임시]: 인정한 시전의 기억은 그 시전의 위협이 다 사라진 뒤에 지운다(위협이 살아 있는 동안은 지우지 않는다).
        [Test]
        public void S01_8_1__GrazedCastForgottenOnlyAfterThreatsGone()
        {
            var k = Kit.Create();
            var shot = new EnemyShotEntity { Id = 50, CastId = 7 };
            k.W.EnemyShots.Add(shot);
            Assert.AreEqual(1, GrazeOnce(k, Kit.Bullet(7, 0.5f, 50)));
            k.Step(700);
            Assert.IsTrue(k.P.GrazedCasts.Contains(7), "탄이 살아 있으면 기억한다");
            shot.Dead = true;
            k.Step(600);
            Assert.IsTrue(k.P.GrazedCasts.Contains(7), "사라진 뒤 10초(600틱)까지는 기억한다");
            k.Step(2);
            Assert.IsFalse(k.P.GrazedCasts.Contains(7));
        }

        // 10 요약서 1.2절: 스침도 콤보 끊김 타이머를 다시 채운다 [임시].
        [Test]
        public void S01_9_2__GrazeRefillsComboTimer()
        {
            var k = Kit.Create();
            k.P.Combo = 3;
            k.Step(100);
            Assert.AreEqual(100, k.P.ComboIdleTicks);
            GrazeOnce(k, Kit.Bullet(1, 0.5f));
            Assert.AreEqual(5, k.P.Combo);
            Assert.AreEqual(1, k.P.ComboIdleTicks, "그 틱의 끝에 1");
            k.Step(118);
            Assert.AreEqual(5, k.P.Combo);
            k.Step();
            Assert.AreEqual(0, k.P.Combo);
        }
    }
}
