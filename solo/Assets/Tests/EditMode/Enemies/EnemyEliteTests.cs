using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Enemies;

namespace Wizard.Solo.Tests.Enemies
{
    /// <summary>엘리트 12종의 고유 능력. 05 요약서 7절(elites.json), 10 요약서 8절 Q-23·Q-24.</summary>
    public class EnemyEliteTests
    {
        static List<ShotRecord> ShotsOf(Kit k, int ownerId, int tick)
        {
            var r = new List<ShotRecord>();
            for (int i = 0; i < k.Threats.Shots.Count; i++)
                if (k.Threats.Shots[i].Tick == tick && k.Threats.Shots[i].Spec.OwnerEnemyId == ownerId) r.Add(k.Threats.Shots[i]);
            return r;
        }

        static void AssertAngles(List<ShotRecord> shots, float[] expected)
        {
            Assert.AreEqual(expected.Length, shots.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < shots.Count; j++)
                    if (Kit.AngleDelta(Kit.Angle(shots[j].Spec.Dir), expected[i]) < 0.02f) found = true;
                Assert.IsTrue(found, "각도 " + expected[i] + "도 탄이 없다");
            }
        }

        // split: 죽으면 슬라임 3마리, 사망 위치 + 반지름 0.5, 120도 간격, 시작각 0. 각 20 × H(w)(엘리트 아님), 보상 0.
        [Test]
        public void S05_ELITE_SPLIT__ThreeSlimesAt120Degrees()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2.32f;
            k.W.Wave.Wave = 12;
            var king = k.Spawn(Ids.Enemy.SlimeMoss, 6f, 6f, elite: true);
            king.PendingDeath = true;
            k.Sys.Stage10_Deaths(k.W);
            Assert.AreEqual(4, k.W.Enemies.Count);
            float s = 0.5f * Mathf.Sin(120f * Mathf.Deg2Rad);
            // 화면 각도(반시계 +, 위가 y−): 0도 → (+0.5, 0), 120도 → (−0.25, −0.433), 240도 → (−0.25, +0.433)
            var expected = new[] { new Vector2(6.5f, 6f), new Vector2(5.75f, 6f - s), new Vector2(5.75f, 6f + s) };
            for (int i = 0; i < 3; i++)
            {
                var c = k.W.Enemies[1 + i];
                Kit.AreClose(expected[i], c.Pos);
                Assert.AreEqual(Ids.Enemy.SlimeMoss, c.DefId);
                Assert.IsFalse(c.IsElite);
                Assert.IsTrue(c.NoReward);
                Assert.AreEqual(king.Id, c.SummonerId);
                Assert.AreEqual(20f * 2.32f, c.MaxHp, 1e-3f);
                Assert.AreEqual(EnemyAiState.Act, c.Ai, "등장 예고 없이 바로");
                Assert.AreEqual(1, c.Cost);
            }
            Assert.IsTrue(king.Dead);
        }

        // howl(Q-24): 돌진 조건이 되면 울부짖음부터. 60틱 뒤 반경 6 안의 일반 늑대(추적 중)를 거리 오름차순으로 h + 60 + 15·i틱에 돌진 요청.
        // 자신은 돌진 2회 연속: 첫 돌진 뒤 경직 없이 둘째 예고 30틱, 둘째 뒤 경직 90틱.
        [Test]
        public void S05_ELITE_HOWL__PackDashesAtHPlus60Plus15i()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(1f, 7f);
            var far = k.Spawn(Ids.Enemy.WolfBramble, 12.2f, 7f);    // 우두머리에서 5.8칸: 둘째
            var alpha = k.Spawn(Ids.Enemy.WolfBramble, 6.4f, 7f, elite: true);   // 플레이어에서 5.4칸
            var near = k.Spawn(Ids.Enemy.WolfBramble, 6.4f, 10f);   // 3칸: 첫째(id는 더 크다)
            var outside = k.Spawn(Ids.Enemy.WolfBramble, 6.4f, 0.5f); // 6.5칸: 반경 밖
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 7f, 8f);        // 늑대가 아니다
            far.MoveSpeed = 0f; near.MoveSpeed = 0f; outside.MoveSpeed = 0f; slime.MoveSpeed = 0f; alpha.MoveSpeed = 0f;

            const int h = 0;
            k.RunThrough(h + 59);
            Assert.AreEqual(0, k.Threats.Tickets.Count, "울부짖는 동안 요청 없음");
            Assert.AreEqual(BrainSub.Howl, Kit.Brain(alpha).Sub);

            k.RunThrough(h + 60);
            Assert.AreEqual(1, k.Threats.CountOf(near.Id));
            Assert.AreEqual(h + 60, k.Threats.LastOf(near.Id).RequestTick, "i = 0");
            Assert.AreEqual(1, k.Threats.CountOf(alpha.Id), "자신도 그 뒤 돌진");
            Assert.AreEqual(0, k.Threats.CountOf(far.Id));

            k.RunThrough(h + 74);
            Assert.AreEqual(0, k.Threats.CountOf(far.Id));
            k.RunThrough(h + 75);
            Assert.AreEqual(1, k.Threats.CountOf(far.Id));
            Assert.AreEqual(h + 75, k.Threats.LastOf(far.Id).RequestTick, "i = 1");

            k.RunThrough(h + 400);
            Assert.AreEqual(0, k.Threats.CountOf(outside.Id));
            Assert.AreEqual(0, k.Threats.CountOf(slime.Id));
        }

        [Test]
        public void S05_ELITE_HOWL__AlphaChainsTwoDashesWithoutStaggerBetween()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(1f, 7f);
            var alpha = k.Spawn(Ids.Enemy.WolfBramble, 6.4f, 7f, elite: true);
            // 첫 돌진: 요청 60 → 판정 90 → 돌진 90~109
            k.RunThrough(89);
            Assert.IsFalse(alpha.DashActive);
            k.RunThrough(90);
            Assert.IsTrue(alpha.DashActive);
            k.RunThrough(109);
            Assert.IsTrue(alpha.DashActive);
            // 110틱: 경직 없이 둘째 예고 30틱
            k.RunThrough(110);
            Assert.IsFalse(alpha.DashActive);
            Assert.AreEqual(2, k.Threats.CountOf(alpha.Id));
            Assert.AreEqual(110, k.Threats.LastOf(alpha.Id).RequestTick);
            Assert.AreEqual(EnemyAiState.WindUp, alpha.Ai);
            Assert.AreEqual(30, k.Threats.LastOf(alpha.Id).Req.Zones[0].TelegraphTicks);
            k.RunThrough(139);
            Assert.IsFalse(alpha.DashActive);
            k.RunThrough(140);
            Assert.IsTrue(alpha.DashActive);
            // 둘째 돌진은 20틱 안에 벽에 닿아도, 안 닿아도 160틱에는 끝나 경직 90틱
            k.RunThrough(160);
            Assert.IsFalse(alpha.DashActive);
            Assert.AreEqual(EnemyAiState.Recovery, alpha.Ai);
            Assert.AreEqual(2, k.Threats.CountOf(alpha.Id));
        }

        // shards: 내려찍기 판정 틱에 그 원 중심에서 파편 8발(45도, 첫 탄 플레이어 쪽), 속도 5, 반지름 0.18, 사거리 9, 피해 1, 스침 대상
        [Test]
        public void S05_ELITE_SHARDS__EightShardsFromSlamCentreAtDetonation()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 8f, 7f, elite: true);
            k.RunBefore(10);
            k.P.Pos = new Vector2(15f, 7f);        // 원 중심(12, 7)에서 +x 쪽
            k.RunThrough(65);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            k.RunThrough(66);
            var shots = ShotsOf(k, g.Id, 66);
            Assert.AreEqual(8, shots.Count);
            Kit.AreClose(new Vector2(1, 0), shots[0].Spec.Dir);
            AssertAngles(shots, new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f });
            for (int i = 0; i < shots.Count; i++)
            {
                Kit.AreClose(new Vector2(12f, 7f), shots[i].Spec.Pos);
                Assert.AreEqual(5f, shots[i].Spec.Speed, 1e-5f);
                Assert.AreEqual(0.18f, shots[i].Spec.Radius, 1e-6f);
                Assert.AreEqual(9f, shots[i].Spec.Range, 1e-5f);
                Assert.AreEqual(1, shots[i].Spec.BaseDamage);
                Assert.IsTrue(shots[i].Spec.Grazeable);
            }
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
        }

        // 휩쓸기에는 파편이 없다(follows 0 = 내려찍기만)
        [Test]
        public void S05_ELITE_SHARDS__SweepHasNoShards()
        {
            var k = new Kit();
            k.Spawn(Ids.Enemy.GolemMenhir, 9.5f, 7f, elite: true);
            k.RunThrough(60);
            Assert.AreEqual(0, k.Threats.Shots.Count);
        }

        // wide_spread: 3갈래 → 5갈래 20도, 나머지 동일(시전 24틱, 속도 6.5)
        [Test]
        public void S05_ELITE_WIDE_SPREAD__FiveShotsTwentyDegrees()
        {
            var k = new Kit();
            var s = k.Spawn(Ids.Enemy.ShamanCap, 6f, 7f, elite: true);
            k.RunThrough(24);
            var shots = ShotsOf(k, s.Id, 24);
            AssertAngles(shots, new[] { 0f, 20f, 40f, 320f, 340f });
            Assert.AreEqual(6.5f, shots[0].Spec.Speed, 1e-5f);
            k.RunThrough(60);
            Assert.AreEqual(EnemyAiState.Act, s.Ai);
        }

        // big_blast: 점화 r2.6, 예고 66틱, 유폭 60 × H(w)(지연 24틱, 반경도 2.6)
        [Test]
        public void S05_ELITE_BIG_BLAST__RadiusTelegraphAndSympathetic()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2f;
            var bug = k.Spawn(Ids.Enemy.BugLantern, 10.8f, 7f, elite: true);
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(2.6f, z.Radius, 1e-6f);
            Assert.AreEqual(66, z.TelegraphTicks);
            Assert.AreEqual(1, z.BaseDamage);

            k = new Kit();
            k.Waves.Mul = wv => 2f;
            k.Threats.BlockAll = true;
            k.P.Pos = new Vector2(5.5f, 5f);
            var king = k.Spawn(Ids.Enemy.BugLantern, 5f, 5f, elite: true);
            var inBig = k.Spawn(Ids.Enemy.SlimeMoss, 7.8f, 5f);      // 2.8 < 2.6 + 0.35 (바탕 1.8이면 밖)
            inBig.MoveSpeed = 0f;
            king.PendingDeath = true;
            k.RunThrough(24);
            Assert.AreEqual(1, k.Spells.Direct.Count);
            Assert.AreEqual(inBig.Id, k.Spells.Direct[0].Damage.TargetEnemyId);
            Assert.AreEqual(60f * 2f, k.Spells.Direct[0].Damage.Amount, 1e-4f);
            Assert.AreEqual(24, k.Spells.Direct[0].Tick);
        }

        // 엘리트는 빙결돼도 예고가 유지된다(시간 절반)
        [Test]
        public void S05_ELITE__FreezeHalvedAndTelegraphKept()
        {
            var k = new Kit();
            var bug = k.Spawn(Ids.Enemy.BugLantern, 10.8f, 7f, elite: true);
            k.RunThrough(9);
            Assert.AreEqual(30f, k.Sys.ApplyCc(k.W, bug, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(0, k.Threats.CancelOwnerCalls.Count);
            Assert.IsFalse(k.Threats.Tickets[0].Cancelled);
            Assert.AreEqual(EnemyAiState.WindUp, bug.Ai);
            k.RunThrough(67);
            Assert.IsTrue(bug.Dead, "점화는 예정대로(66틱) 터진다");
        }

        // triple_aim: 조준선 3개를 s, s+30, s+60에 시작(각 60틱, 마지막 24틱 고정), 각자 요청. 빈틈 84는 마지막 발사 뒤.
        [Test]
        public void S05_ELITE_TRIPLE_AIM__ThreeLinesThirtyTicksApart()
        {
            var k = new Kit();
            var hog = k.Spawn(Ids.Enemy.HogNeedle, 4f, 7f, elite: true);
            k.RunThrough(130);
            Assert.AreEqual(3, k.Threats.Tickets.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(30 * i, k.Threats.Tickets[i].RequestTick);
                Assert.AreEqual(1, k.Threats.Tickets[i].Req.Zones.Count, "각자 요청");
                ZoneSpec z = k.Threats.Tickets[i].Req.Zones[0];
                Assert.AreEqual(ZoneShape.AimLine, z.Shape);
                Assert.AreEqual(60, z.TelegraphTicks);
                Assert.AreEqual(24, z.LockLastTicks);
            }
            Assert.AreEqual(3, k.Threats.Shots.Count);
            Assert.AreEqual(60, k.Threats.Shots[0].Tick);
            Assert.AreEqual(90, k.Threats.Shots[1].Tick);
            Assert.AreEqual(120, k.Threats.Shots[2].Tick);
            Assert.AreEqual(16f, k.Threats.Shots[0].Spec.Speed, 1e-5f);
            Assert.AreNotEqual(k.Threats.Shots[0].Spec.CastId, k.Threats.Shots[1].Spec.CastId);
            Assert.AreEqual(EnemyAiState.Recovery, hog.Ai);
            k.RunThrough(120 + 83);
            Assert.AreEqual(EnemyAiState.Recovery, hog.Ai);
            k.RunThrough(120 + 84);
            Assert.AreEqual(EnemyAiState.Act, hog.Ai);
        }

        // earth_bands: 솟구침 판정 틱에 솟은 자리에서 흙 띠 4개(+x, +y, −x, −y), 폭 0.8, 길이 3, 예고 36틱, 피해 1. 한 묶음(전부 아니면 전무).
        [Test]
        public void S05_ELITE_EARTH_BANDS__FourBandsOneRequestAtEruption()
        {
            var k = new Kit();
            var wz = k.Spawn(Ids.Enemy.WeaselPot, 10.5f, 7f, elite: true);
            k.RunThrough(41);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(42);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            var t = k.Threats.Tickets[1];
            Assert.AreEqual(42, t.RequestTick);
            Assert.AreEqual(wz.Id, t.Req.OwnerEnemyId);
            Assert.IsFalse(t.Req.CountAsOne, "띠 넷은 상한에 넷으로 센다");
            Assert.AreEqual(4, t.Req.Zones.Count);
            var want = new List<float> { 0f, 90f, 180f, 270f };
            for (int i = 0; i < 4; i++)
            {
                ZoneSpec z = t.Req.Zones[i];
                Assert.AreEqual(ZoneShape.Band, z.Shape);
                Assert.AreEqual(0.8f, z.Width, 1e-6f);
                Assert.AreEqual(3f, z.Length, 1e-6f);
                Assert.AreEqual(36, z.TelegraphTicks);
                Assert.AreEqual(1, z.BaseDamage);
                Assert.AreEqual(ZoneAnchor.Fixed, z.Anchor);
                Assert.IsFalse(z.AimAtPlayer);
                Kit.AreClose(new Vector2(12f, 7f), z.Pos);
                float a = Kit.Angle(z.Dir);
                int at = want.FindIndex(x => Kit.AngleDelta(x, a) < 0.02f);
                Assert.GreaterOrEqual(at, 0, "방향 " + a);
                want.RemoveAt(at);
            }
            Assert.IsFalse(wz.Burrowed, "주인은 띠를 기다리지 않고 지상 상태로 간다");
        }

        // summon: 체력 60%, 20% 아래로 처음 내려갈 때 박쥐 4마리(반지름 1, 90도, 보상 0). 최대 2회.
        [Test]
        public void S05_ELITE_SUMMON__FourBatsAtSixtyAndTwentyPercent()
        {
            var k = new Kit();
            var queen = k.Spawn(Ids.Enemy.BatDusk, 3f, 3f, elite: true);
            Assert.AreEqual(12f * 3f, queen.MaxHp, 1e-4f);
            queen.Hp = queen.MaxHp * 0.61f;
            k.W.Step();
            Assert.AreEqual(1, k.W.Enemies.Count);
            queen.Hp = queen.MaxHp * 0.59f;
            Vector2 at = queen.Pos;
            k.W.Step();
            Assert.AreEqual(5, k.W.Enemies.Count);
            var want = new[] { at + new Vector2(1, 0), at + new Vector2(0, -1), at + new Vector2(-1, 0), at + new Vector2(0, 1) };
            for (int i = 0; i < 4; i++)
            {
                var c = k.W.Enemies[1 + i];
                Assert.AreEqual(Ids.Enemy.BatDusk, c.DefId);
                Assert.IsFalse(c.IsElite);
                Assert.IsTrue(c.NoReward);
                Assert.AreEqual(queen.Id, c.SummonerId);
                Kit.AreClose(want[i], c.Pos);
            }
            k.RunThrough(k.W.Tick + 30);
            Assert.AreEqual(5, k.W.Enemies.Count, "같은 문턱에서 다시 부르지 않는다");
            queen.Hp = queen.MaxHp * 0.19f;
            k.W.Step();
            Assert.AreEqual(9, k.W.Enemies.Count);
            queen.Hp = queen.MaxHp * 0.01f;
            k.RunThrough(k.W.Tick + 30);
            Assert.AreEqual(9, k.W.Enemies.Count, "최대 2회");
        }

        [Test]
        public void S05_ELITE_SUMMON__CrossingBothThresholdsInOneTickSummonsTwice()
        {
            var k = new Kit();
            var queen = k.Spawn(Ids.Enemy.BatDusk, 3f, 3f, elite: true);
            queen.Hp = queen.MaxHp * 0.1f;
            k.W.Step();
            Assert.AreEqual(8, k.Sys.CountSummonedBy(k.W, queen.Id));
        }

        // 급강하 dive: Band 0.8 × 5, 예고 30틱, 시작 순간 고정, 속도 18 → 17틱, 피해 1, is_dash. 쿨 240틱, 빈틈 0틱(Q-23).
        [Test]
        public void S05_ELITE_DIVE__SeventeenTickDiveNoRecovery()
        {
            var k = new Kit();
            var queen = k.Spawn(Ids.Enemy.BatDusk, 8f, 7f, elite: true);      // d = 4 ≤ 5
            k.RunThrough(0);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.AreEqual(0.8f, z.Width, 1e-6f);
            Assert.AreEqual(5f, z.Length, 1e-6f);
            Assert.AreEqual(30, z.TelegraphTicks);
            Assert.AreEqual(0, z.LockLastTicks);
            Assert.AreEqual(0, z.BaseDamage);
            Vector2 start = queen.Pos;
            k.RunThrough(29);
            Assert.IsFalse(queen.DashActive);
            Kit.AreClose(start, queen.Pos);
            k.RunThrough(30);
            Assert.IsTrue(queen.DashActive);
            Assert.AreEqual(1, queen.DashBaseDamage);
            k.RunThrough(30 + 16);
            Assert.IsTrue(queen.DashActive);
            Assert.AreEqual(17 * 18f / 60f, (queen.Pos - start).magnitude, 2e-3f, "17틱 × 0.3칸");
            k.RunThrough(30 + 17);
            Assert.IsFalse(queen.DashActive);
            Assert.AreEqual(EnemyAiState.Act, queen.Ai, "빈틈 0틱");
            Assert.AreEqual(30 + 240, Kit.Brain(queen).NextAttackTick);
            var hits = k.Player.AcceptedHits();
            Assert.AreEqual(PlayerDamageSource.Dash, hits[0].Hit.Source);
            // 다음 급강하는 240틱 뒤
            k.RunThrough(30 + 239);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
        }

        // 박쥐 여왕의 급강하는 땅 높이라 병풍에 부딪힌다(엘리트 → 경직 45틱은 Arts → ApplyCc의 절반 규칙)
        [Test]
        public void S05_ELITE_DIVE__HitsIceScreenAndEliteStaggerHalved()
        {
            var k = new Kit();
            var queen = k.Spawn(Ids.Enemy.BatDusk, 8f, 7f, elite: true);
            k.Arts.Hits = (a, b) => a.x < 9f && b.x >= 9f;
            k.Arts.Breaks = true;
            while (k.Arts.Touched.Count == 0 && k.W.Tick < 60) k.W.Step();
            Assert.AreEqual(1, k.Arts.Touched.Count);
            Assert.IsTrue(k.Arts.Touched[0].Dash);
            Assert.GreaterOrEqual(k.Arts.Touched[0].Tick, 30, "급강하 중에 닿았다");
            Assert.Less(queen.Pos.x, 9f);
            Assert.IsFalse(queen.DashActive, "병풍에서 급강하가 끝난다");
            Assert.AreEqual(45f, k.Sys.ApplyCc(k.W, queen, CcKind.Stagger, 90f, CcSource.IceScreen, Vector2.zero));
        }

        // twin_ring: 고리 2회, 30틱 간격, 두 번째 +22.5도. 빈틈은 둘째 고리 뒤.
        [Test]
        public void S05_ELITE_TWIN_RING__SecondRingThirtyTicksLaterRotated()
        {
            var k = new Kit();
            var wisp = k.Spawn(Ids.Enemy.WispStray, 6f, 7f, elite: true);
            k.RunThrough(36);
            AssertAngles(ShotsOf(k, wisp.Id, 36), new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f });
            Assert.AreEqual(EnemyAiState.WindUp, wisp.Ai);
            k.RunThrough(65);
            Assert.AreEqual(8, k.Threats.Shots.Count);
            k.RunThrough(66);
            AssertAngles(ShotsOf(k, wisp.Id, 66), new[] { 22.5f, 67.5f, 112.5f, 157.5f, 202.5f, 247.5f, 292.5f, 337.5f });
            Assert.AreEqual(EnemyAiState.Recovery, wisp.Ai);
            k.RunThrough(66 + 59);
            Assert.AreEqual(EnemyAiState.Recovery, wisp.Ai);
            k.RunThrough(66 + 60);
            Assert.AreEqual(EnemyAiState.Act, wisp.Ai);
        }

        // bloom: 씨앗 2개 동시(한 묶음): P, P + 마지막 이동 8방향 × 3(정지면 시선). 전장 밖이면 벽 안 0.5칸. 가시밭 6초.
        [Test]
        public void S05_ELITE_BLOOM__TwoSeedsOneRequest()
        {
            var k = new Kit();
            k.P.Facing = Dir8.S;
            k.Spawn(Ids.Enemy.VineThorn, 5f, 5f, elite: true);
            k.RunThrough(0);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            var req = k.Threats.Tickets[0].Req;
            Assert.AreEqual(2, req.Zones.Count);
            Assert.AreEqual(ZoneAnchor.TargetPlayer, req.Zones[0].Anchor);
            Assert.AreEqual(ZoneAnchor.Fixed, req.Zones[1].Anchor);
            Kit.AreClose(new Vector2(12f, 10f), req.Zones[1].Pos);       // 정지 → 시선(S = 화면 아래 = y+) 3칸 앞
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(1.5f, req.Zones[i].Radius, 1e-6f);
                Assert.AreEqual(60, req.Zones[i].TelegraphTicks);
                Assert.AreEqual(6f, req.Zones[i].LeavesHazard.DurationSeconds, 1e-6f);
                Assert.AreEqual(3, req.Zones[i].LeavesHazard.MaxCount, "동시 3개 상한 공통");
            }
        }

        [Test]
        public void S05_ELITE_BLOOM__LeadUsesMoveDirAndClampsInsideWall()
        {
            var k = new Kit();
            k.P.Moving = true;
            k.P.MoveDir = Dir8.E;
            k.Spawn(Ids.Enemy.VineThorn, 5f, 5f, elite: true);
            k.RunThrough(0);
            Kit.AreClose(new Vector2(15f, 7f), k.Threats.Tickets[0].Req.Zones[1].Pos);

            k = new Kit();
            k.P.Pos = new Vector2(12f, 13f);
            k.P.Facing = Dir8.S;
            k.Spawn(Ids.Enemy.VineThorn, 5f, 5f, elite: true);
            k.RunThrough(0);
            Kit.AreClose(new Vector2(12f, k.W.ArenaHeight - 0.5f), k.Threats.Tickets[0].Req.Zones[1].Pos);
        }

        // bounce: 첫 전장 벽 충돌 → 30틱 멈춤 → 벽 법선 반사 방향 새 띠 예고 54틱 → 돌진 → 두 번째 벽에서 기절 120틱(×1.5)
        [Test]
        public void S05_ELITE_BOUNCE__ReflectsOnceThenStunsAtSecondWall()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f, elite: true);
            k.RunThrough(126);
            Assert.IsFalse(boar.DashActive);
            Assert.AreEqual(0, boar.StunUntilTick, "첫 벽에서는 기절하지 않는다");
            Assert.AreEqual(1f, boar.DamageTakenMul, 1e-6f);
            Assert.AreEqual(BrainSub.BounceStop, Kit.Brain(boar).Sub);
            k.RunThrough(126 + 29);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(126 + 30);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            var t = k.Threats.Tickets[1];
            Assert.AreEqual(156, t.RequestTick);
            ZoneSpec z = t.Req.Zones[0];
            Assert.AreEqual(54, z.TelegraphTicks);
            Assert.IsFalse(z.AimAtPlayer);
            Kit.AreClose(new Vector2(-1, 0), z.Dir);
            k.RunThrough(156 + 53);
            Assert.IsFalse(boar.DashActive);
            k.RunThrough(156 + 54);
            Assert.IsTrue(boar.DashActive);
            Kit.AreClose(new Vector2(-1, 0), boar.DashDir);
            // 반대쪽 벽(x = 0.55)까지 22.9칸 → 86틱째 이동(295틱)에 닿아 기절
            k.RunThrough(295);
            Assert.IsFalse(boar.DashActive);
            Assert.AreEqual(boar.BodyRadius, boar.Pos.x, 1e-3f);
            Assert.AreEqual(295 + 120, boar.StunUntilTick);
            Assert.AreEqual(1.5f, boar.DamageTakenMul, 1e-6f);
            Assert.AreEqual(54 + 180, Kit.Brain(boar).NextAttackTick, "재사용 대기는 첫 돌진 기준");
        }

        // big_drum: 울음 반경 6, 끊기 60 × H(w), 빙결·기절로는 취소 안 됨
        [Test]
        public void S05_ELITE_BIG_DRUM__RadiusSixAndBreakOnlyByDamage()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20f, 12f);
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f, elite: true);
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 10.5f, 5f);         // 5.5칸: 바탕(4)이면 밖, 큰북(6)이면 안
            var b = k.Spawn(Ids.Enemy.SlimeMoss, 11.5f, 5f);         // 6.5칸: 밖
            toad.MoveSpeed = 0f; a.MoveSpeed = 0f; b.MoveSpeed = 0f;
            k.RunThrough(400);
            Assert.AreEqual(30f, k.Sys.ApplyCc(k.W, toad, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(BrainSub.Croak, Kit.Brain(toad).Sub, "빙결로는 취소되지 않는다");
            // 59 × H(1)로는 끊기지 않는다
            k.Sys.ModifyIncomingDamage(k.W, toad, 59f, false, false, Vector2.zero, Vector2.zero);
            k.RunThrough(480);
            Assert.IsTrue(a.DrumBuffed);
            Assert.IsFalse(b.DrumBuffed);

            k = new Kit();
            k.P.Pos = new Vector2(20f, 12f);
            toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f, elite: true);
            a = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 5f);
            toad.MoveSpeed = 0f; a.MoveSpeed = 0f;
            k.RunThrough(399);
            k.Sys.ModifyIncomingDamage(k.W, toad, 60f, false, false, Vector2.zero, Vector2.zero);
            k.RunThrough(400);
            // 끊기 경직 60틱은 엘리트 절반 규칙 밖(스스로 멈추는 것)
            Assert.AreEqual(400 + 60, toad.StaggerUntilTick);
            k.RunThrough(480);
            Assert.IsFalse(a.DrumBuffed);
        }
    }
}
