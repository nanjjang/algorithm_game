using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>밤눈(05 요약서 9.4절). Threats·Enemies·Player는 가짜(G07의 BossWorld).</summary>
    public class StormOwlTests
    {
        const string Id = Ids.Boss.StormOwl;

        static void AssertGlideBand(BossWorld bw, ZoneSpec z, Vector2 through)
        {
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.That(z.Width, Is.EqualTo(2f));
            Assert.AreEqual(54, z.TelegraphTicks);
            Assert.AreEqual(0, z.BaseDamage, "활강의 피해는 몸 충돌이 준다");
            Assert.AreEqual(PlayerDamageSource.Dash, z.DamageSource);
            Assert.That(z.Dir.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(FourKit.LineDistance(through, z.Pos, z.Dir), Is.LessThan(0.001f), "중심선이 그 틱의 플레이어 위치를 지난다");
            Assert.IsTrue(FourKit.OnBorder(bw.W, z.Pos), "전장 끝에서 들어온다");
            Assert.IsTrue(FourKit.OnBorder(bw.W, z.Pos + z.Dir * z.Length), "전장을 가로지른다");
        }

        // 9.4절 glide + 계약: 돌진 시작 틱 = 요청 틱 + 예고 54틱, 몸 충돌은 Dash·AttackId = 몸통 Id, 다음 예고 = 앞 활강이 끝난 틱,
        // 3회 연속(축은 직전과 다르다), 세 번째 뒤 착지 120틱 동안 비행을 뗀다.
        [Test]
        public void S06_9_4__GlideThreeTimesTimingCollisionAndLanding()
        {
            var bw = FourKit.Make(Id, 40, StormOwlBrain.PatternGlide);
            var body = bw.Body;
            Assert.IsTrue(body.Has(EnemyTags.Flying));
            bw.P.Pos = new Vector2(9f, 6f);
            bw.FinishIntro();
            int request = FourKit.WaitPattern(bw, StormOwlBrain.PatternGlide);
            var dirs = new List<Vector2>();
            int end = -1;
            for (int n = 0; n < 3; n++)
            {
                Assert.AreEqual(n + 1, bw.Threats.Tickets.Count);
                var t = bw.Threats.Tickets[n];
                Assert.AreEqual(request, t.RequestTick);
                Assert.AreEqual(1, t.Request.Zones.Count);
                var z = t.Request.Zones[0];
                AssertGlideBand(bw, z, bw.P.Pos);
                dirs.Add(z.Dir);

                bw.StepTo(request + 54);
                Assert.IsFalse(body.DashActive);
                Assert.AreEqual(n, FourKit.CountHits(bw, PlayerDamageSource.Dash));
                bw.Step(1);   // 판정 틱: 이 틱부터 몸이 움직인다
                Assert.IsTrue(body.DashActive);
                Assert.AreEqual(2, body.DashBaseDamage);
                Assert.That(Vector2.Angle(body.DashDir, z.Dir), Is.LessThan(0.01f));
                Assert.That(FourKit.LineDistance(body.Pos, z.Pos, z.Dir), Is.LessThan(0.001f), "띠의 중심선 위를 난다");
                Assert.IsTrue(body.Has(EnemyTags.Flying));

                end = FourKit.RunDash(bw);
                // 16칸/초: 가장 긴 대각선(√(22² + 12²) ≈ 25칸)도 100틱 안에 지난다.
                Assert.Less(end - (request + 54), 100);
                Assert.AreEqual(n + 1, FourKit.CountHits(bw, PlayerDamageSource.Dash), "활강 한 번에 플레이어당 한 번");
                request = end;   // 다음 예고는 앞 활강이 끝난 틱
            }
            var hit = FourKit.FirstHit(bw, PlayerDamageSource.Dash);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(body.Id, hit.SourceEnemyId);
            Assert.AreEqual(2, hit.DamageCells);
            // 축: 이웃한 활강끼리 다르다(방향이 평행하지 않다).
            Assert.That(Mathf.Abs(Vector2.Dot(dirs[0], dirs[1])), Is.LessThan(0.99f));
            Assert.That(Mathf.Abs(Vector2.Dot(dirs[1], dirs[2])), Is.LessThan(0.99f));

            // 세 번째 뒤: 예고가 더 없고, 착지 빈틈 120틱 동안 비행을 뗀다.
            Assert.AreEqual(3, bw.Threats.Tickets.Count);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            Assert.IsFalse(body.Has(EnemyTags.Flying));
            Assert.IsTrue(body.Has(EnemyTags.Boss));
            bw.StepTo(end + 120);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            Assert.IsFalse(body.Has(EnemyTags.Flying));
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.IsTrue(body.Has(EnemyTags.Flying));
        }

        // 9.4절 2단계: 활강 두 줄이 동시에 십자(수직인 두 축). 두 줄이 다 플레이어를 지나도 같은 돌진이라 피격 후보는 하나.
        [Test]
        public void S06_9_4__PhaseTwoCrossGlide()
        {
            var bw = FourKit.Make(Id, 40, StormOwlBrain.PatternGlide);
            var body = bw.Body;
            bw.P.Pos = new Vector2(9f, 6f);
            bw.FinishIntro();
            FourKit.ToPhase(bw, 1);
            int request = FourKit.WaitPattern(bw, StormOwlBrain.PatternGlide);
            var t = bw.Threats.Tickets[0];
            Assert.IsTrue(t.Request.CountAsOne);
            Assert.AreEqual(2, t.Request.Zones.Count);
            AssertGlideBand(bw, t.Request.Zones[0], bw.P.Pos);
            AssertGlideBand(bw, t.Request.Zones[1], bw.P.Pos);
            Assert.That(Vector2.Dot(t.Request.Zones[0].Dir, t.Request.Zones[1].Dir), Is.EqualTo(0f).Within(0.0001f), "서로 수직");

            FourKit.RunThrough(bw, request + 54);
            Assert.IsTrue(body.DashActive);
            // 다음 예고가 뜰 때까지(= 두 줄이 다 지나갈 때까지).
            for (int i = 0; i < 300 && bw.Threats.Tickets.Count < 2; i++) bw.Step(1);
            Assert.AreEqual(2, bw.Threats.Tickets.Count);
            Assert.AreEqual(1, FourKit.CountHits(bw, PlayerDamageSource.Dash));
            Assert.AreEqual(body.Id, FourKit.FirstHit(bw, PlayerDamageSource.Dash).AttackId);
            // 다음 십자는 앞 십자와 다른 두 축이다.
            var next = bw.Threats.Tickets[1].Request.Zones;
            Assert.AreEqual(2, next.Count);
            Assert.That(Mathf.Abs(Vector2.Dot(next[0].Dir, t.Request.Zones[0].Dir)), Is.LessThan(0.99f));
            Assert.That(Mathf.Abs(Vector2.Dot(next[0].Dir, t.Request.Zones[1].Dir)), Is.LessThan(0.99f));
        }

        // 십자의 둘째 줄만 플레이어를 지나도(몸은 다른 줄) 돌진 피격 후보가 올라간다: Source = Dash, AttackId = 몸통 Id.
        [Test]
        public void S06_9_4__CrossSecondLineHitsAsSameDash()
        {
            var bw = FourKit.Make(Id, 40, StormOwlBrain.PatternGlide);
            var body = bw.Body;
            bw.P.Pos = new Vector2(9f, 6f);
            bw.FinishIntro();
            FourKit.ToPhase(bw, 1);
            int request = FourKit.WaitPattern(bw, StormOwlBrain.PatternGlide);
            var zones = bw.Threats.Tickets[0].Request.Zones;
            // 예고가 뜬 뒤 둘째 줄 위로만 옮긴다(첫 줄에서 3칸 떨어진 자리).
            Vector2 cross = bw.P.Pos;
            bw.P.Pos = bw.W.ClampToArena(cross + zones[1].Dir * 3f, 0.5f);
            if (FourKit.LineDistance(bw.P.Pos, zones[0].Pos, zones[0].Dir) < 2f) bw.P.Pos = bw.W.ClampToArena(cross - zones[1].Dir * 3f, 0.5f);
            Assert.Greater(FourKit.LineDistance(bw.P.Pos, zones[0].Pos, zones[0].Dir), 2f);
            Assert.Less(FourKit.LineDistance(bw.P.Pos, zones[1].Pos, zones[1].Dir), 0.01f);
            FourKit.RunThrough(bw, request + 54);
            for (int i = 0; i < 300 && bw.Threats.Tickets.Count < 2; i++) bw.Step(1);
            Assert.AreEqual(1, FourKit.CountHits(bw, PlayerDamageSource.Dash));
            var hit = FourKit.FirstHit(bw, PlayerDamageSource.Dash);
            Assert.AreEqual(body.Id, hit.AttackId);
            Assert.AreEqual(2, hit.DamageCells);
        }

        // 9.4절 feather_fan: 날개 접기 30틱 → 5갈래 20도 깃털(속도 7, 소 0.12)을 24틱 간격 3번. 빈틈 48틱.
        [Test]
        public void S06_9_4__FeatherFanThreeVolleys()
        {
            var bw = FourKit.Make(Id, 40, StormOwlBrain.PatternFan);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, StormOwlBrain.PatternFan);
            Assert.AreEqual(0, bw.Threats.Tickets.Count);
            bw.StepTo(r + 30);
            Assert.AreEqual(0, bw.Threats.Shots.Count);
            for (int k = 0; k < 3; k++)
            {
                FourKit.RunThrough(bw, r + 30 + 24 * k);
                Assert.AreEqual(5 * (k + 1), bw.Threats.Shots.Count);
                Assert.AreEqual(r + 30 + 24 * k, bw.Threats.ShotTicks[5 * k]);
            }
            var shots = bw.Threats.Shots;
            Assert.That(shots[0].Speed, Is.EqualTo(7f));
            Assert.That(shots[0].Radius, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.AreEqual(1, shots[0].BaseDamage);
            for (int i = 0; i < 4; i++) Assert.That(Vector2.Angle(shots[i].Dir, shots[i + 1].Dir), Is.EqualTo(20f).Within(0.01f));
            Assert.That(Vector2.Angle(shots[2].Dir, bw.P.Pos - bw.Body.Pos), Is.LessThan(0.01f), "가운데 깃털이 플레이어 쪽");
            int end = r + 30 + 48;
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(end + 48);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.IsTrue(bw.Body.Has(EnemyTags.Flying), "활강이 아닌 빈틈에는 비행을 떼지 않는다");
        }

        // 9.4절 lightning_strike: circle r1.5, 각 48틱, 24틱 간격 4번(2회차 6번). 각 원 = 그 원의 예고 시작 틱의 플레이어 위치.
        [TestCase(40, 4)]
        [TestCase(100, 6)]
        public void S06_9_4__LightningStrikeFollowsPlayer(int wave, int count)
        {
            var bw = FourKit.Make(Id, wave, StormOwlBrain.PatternStrike);
            bw.FinishIntro();
            int r = FourKit.WaitPattern(bw, StormOwlBrain.PatternStrike);
            for (int k = 0; k < count; k++)
            {
                Vector2 at = new Vector2(3f + 2f * k, 4f + k);
                if (k > 0)
                {
                    bw.P.Pos = at;
                    FourKit.RunThrough(bw, r + 24 * k);
                }
                else at = bw.P.Pos;
                Assert.AreEqual(k + 1, bw.Threats.Tickets.Count);
                var t = bw.Threats.Tickets[k];
                Assert.AreEqual(r + 24 * k, t.RequestTick);
                Assert.AreEqual(1, t.Request.Zones.Count);
                var z = t.Request.Zones[0];
                Assert.AreEqual(ZoneShape.Circle, z.Shape);
                Assert.That(z.Radius, Is.EqualTo(1.5f));
                Assert.AreEqual(48, z.TelegraphTicks);
                Assert.AreEqual(1, z.BaseDamage);
                Assert.AreEqual(at, z.Pos);
            }
            // 마지막 원이 터진 틱에 끝난다(빈틈 없음). 그때까지 원이 더 뜨지 않는다.
            int last = r + 24 * (count - 1) + 48;
            bw.StepTo(last);
            Assert.AreEqual(count, bw.Threats.Tickets.Count);
            Assert.AreNotEqual(BossFightState.Waiting, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
            Assert.AreEqual(count, bw.Threats.Tickets.Count);
        }

        // 9.4절 lightning_plow(2단계, T178): 가로 띠 7줄. 묶음 A(짝수 줄 또는 홀수 줄) 예고 0~60·판정 60 / 묶음 B(나머지) 예고 108~168·판정 168 / 빈틈 60.
        [Test]
        public void S06_T178__LightningPlowTwoBatches()
        {
            var bw = FourKit.Make(Id, 40, StormOwlBrain.PatternPlow);
            bw.FinishIntro();
            Assert.IsFalse(bw.Ctx.PhaseDef.Patterns.Contains(StormOwlBrain.PatternPlow), "1단계에는 밭갈이가 없다");
            FourKit.ToPhase(bw, 1);
            int r = FourKit.WaitPattern(bw, StormOwlBrain.PatternPlow);
            Assert.AreEqual(1, bw.Threats.Tickets.Count);
            var a = bw.Threats.Tickets[0];
            Assert.AreEqual(r, a.RequestTick);
            Assert.IsTrue(a.Request.CountAsOne);
            var rows = new List<int>();
            int parityA = ReadRows(bw, a.Request.Zones, rows);
            Assert.That(a.Request.Zones.Count, Is.EqualTo(parityA == 0 ? 4 : 3), "짝수 줄 0,2,4,6 또는 홀수 줄 1,3,5");

            bw.StepTo(r + 60 + 48);
            Assert.AreEqual(1, bw.Threats.Tickets.Count, "둘째 묶음의 예고는 첫 묶음 판정 + 48틱에");
            Assert.AreEqual(BossFightState.Execute, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(2, bw.Threats.Tickets.Count);
            var b = bw.Threats.Tickets[1];
            Assert.AreEqual(r + 108, b.RequestTick);
            int parityB = ReadRows(bw, b.Request.Zones, rows);
            Assert.AreNotEqual(parityA, parityB);
            Assert.AreEqual(7, rows.Count);
            rows.Sort();
            for (int i = 0; i < 7; i++) Assert.AreEqual(i, rows[i], "두 묶음이 일곱 줄을 한 번씩 덮는다");

            bw.StepTo(r + 168);
            Assert.AreEqual(BossFightState.Telegraph, bw.Boss.State);
            bw.Step(1);   // 둘째 묶음 판정
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.StepTo(r + 168 + 60);
            Assert.AreEqual(BossFightState.Recovery, bw.Boss.State);
            bw.Step(1);
            Assert.AreEqual(BossFightState.Waiting, bw.Boss.State);
        }

        /// <summary>띠들의 줄 번호를 rows에 더하고 그 묶음의 홀짝(0 = 짝수 줄)을 돌려준다. 모양·피해·예고 틱도 본다.</summary>
        static int ReadRows(BossWorld bw, List<ZoneSpec> zones, List<int> rows)
        {
            int parity = -1;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                Assert.AreEqual(ZoneShape.Band, z.Shape);
                Assert.That(z.Width, Is.EqualTo(2f));
                Assert.That(z.Length, Is.EqualTo(bw.W.ArenaWidth));
                Assert.That(z.Pos.x, Is.EqualTo(0f));
                Assert.That(Vector2.Angle(z.Dir, new Vector2(1f, 0f)), Is.LessThan(0.01f));
                Assert.AreEqual(60, z.TelegraphTicks);
                Assert.AreEqual(2, z.BaseDamage);
                Assert.AreEqual(PlayerDamageSource.Telegraph, z.DamageSource);
                float rowF = (z.Pos.y - 1f) / 2f;
                int row = Mathf.RoundToInt(rowF);
                Assert.That(rowF, Is.EqualTo(row).Within(0.0001f));
                Assert.That(row, Is.InRange(0, 6));
                if (parity < 0) parity = row % 2;
                Assert.AreEqual(parity, row % 2, "한 묶음은 전부 짝수 줄이거나 전부 홀수 줄");
                rows.Add(row);
            }
            return parity;
        }

        // 공통 규칙 3: 대상 플레이어가 없으면 그 틱은 가만히.
        [Test]
        public void S06_Rule3__NoTargetNoAction()
        {
            var bw = FourKit.Make(Id, 40, null, still: false);
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
