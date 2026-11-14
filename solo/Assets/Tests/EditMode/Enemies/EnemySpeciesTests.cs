using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Enemies;

namespace Wizard.Solo.Tests.Enemies
{
    /// <summary>일반 적 12종 + 졸개의 종별 두뇌. 05 요약서 6.1~6.13절, 10 요약서 8절 Q-25·Q-26. 틱은 요청 틱 기준으로 센다.</summary>
    public class EnemySpeciesTests
    {
        // ------------------------------------------------------------ 6.1 이끼 슬라임, 6.13 졸개

        // 직선 추적 3.0칸/초 = 0.05칸/틱
        [Test]
        public void S05_SLIME__ChasesAtThreeTilesPerSecond()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20, 7);
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 4, 7);
            k.RunThrough(0);
            Assert.AreEqual(4.05f, e.Pos.x, 1e-4f);
            k.RunThrough(59);
            Assert.AreEqual(7f, e.Pos.x, 1e-3f);
            Assert.AreEqual(7f, e.Pos.y, 1e-4f);
            Assert.AreEqual(0, k.Threats.Tickets.Count, "예고 없음");
        }

        // Q-26: 갓버섯 새싹 = 슬라임과 같은 두뇌(직선 추적 + 접촉), 속도 2.5, 접촉 1, 반지름 0.25
        [Test]
        public void S10_Q26__SeedlingUsesSlimeBrain()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20, 7);
            var e = k.Spawn(Ids.Enemy.SporeSeedling, 4, 7);
            Assert.AreEqual(0.25f, e.BodyRadius, 1e-6f);
            Assert.AreEqual(EnemyTags.None, e.Tags);
            Assert.AreEqual(Ids.Element.Neutral, e.Weak);
            k.RunThrough(59);
            Assert.AreEqual(6.5f, e.Pos.x, 1e-3f);
            // 접촉 1
            k.P.Pos = e.Pos + new Vector2(0.2f, 0);
            k.W.Step();
            var hits = k.Player.AcceptedHits();
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(PlayerDamageSource.Contact, hits[0].Hit.Source);
            Assert.AreEqual(1, hits[0].Hit.DamageCells);
        }

        // 북두꺼비 버프 시 슬라임 3.6
        [Test]
        public void S05_SLIME__BuffedSpeedThreePointSix()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20, 7);
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 4, 7);
            GiveBuff(e);
            k.RunThrough(59);
            Assert.AreEqual(7.6f, e.Pos.x, 1e-3f);
        }

        static void GiveBuff(EnemyState e)
        {
            var toad = TestContent.Db.Enemy(Ids.Enemy.ToadDrum).Ai;
            var b = Kit.Brain(e);
            e.DrumBuffed = true;
            b.BuffUntilTick = int.MaxValue;
            b.BuffSpeedRatio = (float)toad["buff_speed_ratio"];
            b.BuffSpeedCap = (float)toad["buff_speed_cap"];
            b.BuffRecoveryRatio = (float)toad["buff_recovery_ratio"];
        }

        // ------------------------------------------------------------ 6.2 덤불 늑대

        // 발동 거리 3.5 ≤ d ≤ 5.5
        [TestCase(3.4f, false)]
        [TestCase(3.5f, true)]
        [TestCase(4.5f, true)]
        [TestCase(5.5f, true)]
        [TestCase(5.6f, false)]
        public void S05_WOLF__TriggerDistance(float d, bool requests)
        {
            var k = new Kit();
            k.Spawn(Ids.Enemy.WolfBramble, 12f - d, 7f);
            k.RunThrough(0);
            Assert.AreEqual(requests ? 1 : 0, k.Threats.Tickets.Count);
        }

        // 요청 틱 T(바로 허가) → 예고 30틱(시작 순간 방향 고정) → T + 30의 7단계에 돌진 시작, 그 틱 9단계부터 20틱 동안 18칸/초 → 경직 90틱 → 추적
        [Test]
        public void S05_WOLF__StateMachineTimeline()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            var t = k.Threats.Tickets[0];
            Assert.AreEqual(0, t.RequestTick);
            ZoneSpec z = t.Req.Zones[0];
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.AreEqual(1.0f, z.Width, 1e-6f);
            Assert.AreEqual(6f, z.Length, 1e-6f);
            Assert.AreEqual(30, z.TelegraphTicks);
            Assert.AreEqual(0, z.LockLastTicks, "예고 시작 순간 방향 고정");
            Assert.AreEqual(ZoneAnchor.Owner, z.Anchor);
            Assert.IsTrue(z.AimAtPlayer);
            Assert.AreEqual(PlayerDamageSource.Dash, z.DamageSource);

            // 예고 중에 플레이어가 움직여도 방향은 그대로다.
            k.RunBefore(15);
            k.P.Pos = new Vector2(9.5f, 3f);
            k.RunThrough(29);
            Assert.IsFalse(wolf.DashActive);
            Assert.AreEqual(EnemyAiState.WindUp, wolf.Ai);
            Kit.AreClose(new Vector2(5f, 7f), wolf.Pos);

            k.RunThrough(30);
            Assert.IsTrue(wolf.DashActive, "돌진 시작 틱 = 요청 틱 + 30");
            Assert.AreEqual(EnemyAiState.Strike, wolf.Ai);
            Assert.AreEqual(1, wolf.DashBaseDamage);
            Assert.AreEqual(t.Req.CastId, wolf.DashCastId);
            Kit.AreClose(new Vector2(1, 0), wolf.DashDir);
            Kit.AreClose(new Vector2(5.3f, 7f), wolf.Pos);

            k.RunThrough(49);
            Assert.IsTrue(wolf.DashActive);
            Kit.AreClose(new Vector2(11f, 7f), wolf.Pos, 2e-3f);   // 20틱 × 0.3칸 = 6칸

            k.RunThrough(50);
            Assert.IsFalse(wolf.DashActive);
            Assert.AreEqual(EnemyAiState.Recovery, wolf.Ai);
            Kit.AreClose(new Vector2(11f, 7f), wolf.Pos, 2e-3f);

            k.RunThrough(139);
            Assert.AreEqual(EnemyAiState.Recovery, wolf.Ai);
            k.RunThrough(140);
            Assert.AreEqual(EnemyAiState.Act, wolf.Ai, "경직 90틱");
        }

        // 버프 시 경직 63틱, 돌진 속도는 그대로
        [Test]
        public void S05_WOLF__BuffedStagger63DashSpeedUnchanged()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            GiveBuff(wolf);
            k.RunThrough(49);
            Kit.AreClose(new Vector2(11f, 7f), wolf.Pos, 2e-3f);
            k.RunThrough(50 + 62);
            Assert.AreEqual(EnemyAiState.Recovery, wolf.Ai);
            k.RunThrough(50 + 63);
            Assert.AreEqual(EnemyAiState.Act, wolf.Ai);
        }

        // 돌진 중 벽에 닿으면 멈추고 보통 경직(기절 아님, U-29)
        [Test]
        public void S05_WOLF__DashStopsAtWallWithNormalStagger()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(23.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 19.5f, 7f);
            k.RunThrough(49);
            Assert.AreEqual(k.W.ArenaWidth - wolf.BodyRadius, wolf.Pos.x, 1e-3f);
            Assert.IsFalse(wolf.DashActive);
            Assert.AreEqual(0, wolf.StunUntilTick);
            Assert.AreEqual(1f, wolf.DamageTakenMul);
            Assert.AreEqual(EnemyAiState.Recovery, wolf.Ai);
        }

        // WindUp ─(② 반동 사격)→ 예고 취소 + 경직 48틱. 꼬리표 없는 덤불 늑대에게만.
        [Test]
        public void S05_WOLF__RecoilCancelsDashTelegraphWith48TickStagger()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            wolf.MoveSpeed = 0f;
            Assert.IsFalse(k.Sys.TryCancelDash(k.W, wolf, 48), "추적 중에는 끊을 돌진이 없다");
            k.RunThrough(9);
            Assert.IsTrue(k.Sys.TryCancelDash(k.W, wolf, 48));
            Assert.AreEqual(10 + 48, wolf.StaggerUntilTick);
            CollectionAssert.Contains(k.Threats.CancelOwnerCalls, wolf.Id);
            Assert.IsTrue(k.Threats.Tickets[0].Cancelled);
            Assert.AreEqual(EnemyAiState.Interrupted, wolf.Ai);
            k.RunThrough(57);
            Assert.IsFalse(wolf.DashActive, "취소된 돌진은 나가지 않는다");
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(58);
            Assert.AreEqual(2, k.Threats.Tickets.Count, "경직이 풀리면 다시 추적 → 돌진 요청");
        }

        [Test]
        public void S05_WOLF__RecoilCancelOnlyForUntaggedNormal()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 5f, 7f);
            var bug = k.Spawn(Ids.Enemy.BugLantern, 9.5f, 6f);
            var alpha = k.Spawn(Ids.Enemy.WolfBramble, 5f, 8f, elite: true);
            k.RunThrough(20);
            Assert.AreEqual(EnemyAiState.WindUp, boar.Ai);
            Assert.IsFalse(k.Sys.TryCancelDash(k.W, boar, 48), "중량");
            k.RunThrough(62);
            Assert.AreEqual(EnemyAiState.WindUp, alpha.Ai);
            Assert.IsFalse(k.Sys.TryCancelDash(k.W, alpha, 48), "엘리트");
            var bug2 = k.Spawn(Ids.Enemy.BugLantern, 9.5f, 7.8f);
            k.RunThrough(64);
            Assert.AreEqual(EnemyAiState.WindUp, bug2.Ai);
            Assert.IsFalse(k.Sys.TryCancelDash(k.W, bug2, 48), "돌진이 아닌 예고");
            Assert.AreEqual(0, k.Threats.CancelOwnerCalls.Count - CountOf(k.Threats.CancelOwnerCalls, bug.Id));
        }

        static int CountOf(List<int> list, int v)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] == v) n++;
            return n;
        }

        // WindUp ─(빙결)→ 예고 취소 → Frozen → Chase
        [Test]
        public void S05_WOLF__FreezeCancelsTelegraph()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            wolf.MoveSpeed = 0f;
            k.RunThrough(9);
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, wolf, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.IsTrue(k.Threats.Tickets[0].Cancelled);
            Assert.AreEqual(EnemyAiState.Interrupted, wolf.Ai);
            k.RunThrough(69);
            Assert.IsFalse(wolf.DashActive);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(70);
            Assert.AreEqual(2, k.Threats.Tickets.Count, "풀리면 다시 추적");
        }

        // 회피 대시: 1.5칸을 6틱에(15칸/초), 재사용 90틱, 추적 중에만
        [Test]
        public void S05_WOLF__DodgeDashOnePointFiveTilesInSixTicks()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(5f, 13f);       // 거리 8: 돌진 조건 밖
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 5f);
            wolf.MoveSpeed = 0f;
            k.W.Projectiles.Add(new ProjectileEntity
            {
                Id = k.W.NewEntityId(), Block = TestContent.Db.Block(Ids.Block.MagicBolt), State = ProjectileState.Flying,
                Pos = new Vector2(2f, 5f), Dir = new Vector2(1, 0), Speed = 14f, Radius = 0.12f
            });
            k.RunThrough(0);
            Assert.AreEqual(BrainSub.Dodge, Kit.Brain(wolf).Sub);
            Assert.AreEqual(90, Kit.Brain(wolf).DodgeReadyTick);
            Kit.AreClose(new Vector2(5f, 4.75f), wolf.Pos);     // 정면이면 탄 진행 방향의 왼쪽(화면 위)
            k.RunThrough(5);
            Kit.AreClose(new Vector2(5f, 3.5f), wolf.Pos);
            k.RunThrough(40);
            Kit.AreClose(new Vector2(5f, 3.5f), wolf.Pos);
            Assert.AreEqual(BrainSub.None, Kit.Brain(wolf).Sub);
            Assert.AreEqual(0, k.Player.Hits.Count);
        }

        // 즉발·ignores_wolf_dodge(연쇄 번개)는 못 피한다
        [Test]
        public void S05_WOLF__CannotDodgeInstantOrIgnoreFlag()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(5f, 13f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 5f);
            wolf.MoveSpeed = 0f;
            var chain = TestContent.Db.Block(Ids.Block.ChainLightning);
            Assert.IsTrue(chain.Instant && chain.HasFlag("ignores_wolf_dodge"));
            k.W.Projectiles.Add(new ProjectileEntity
            {
                Id = k.W.NewEntityId(), Block = chain, State = ProjectileState.Flying,
                Pos = new Vector2(2f, 5f), Dir = new Vector2(1, 0), Speed = 14f, Radius = 0.12f
            });
            k.RunThrough(10);
            Kit.AreClose(new Vector2(5f, 5f), wolf.Pos);
        }

        // 회피는 추적 중에만: 돌진 예고 중에는 피하지 않는다
        [Test]
        public void S05_WOLF__NoDodgeDuringWindUp()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(3);
            Assert.AreEqual(EnemyAiState.WindUp, wolf.Ai);
            k.W.Projectiles.Add(new ProjectileEntity
            {
                Id = k.W.NewEntityId(), Block = TestContent.Db.Block(Ids.Block.MagicBolt), State = ProjectileState.Flying,
                Pos = new Vector2(7f, 7f), Dir = new Vector2(-1, 0), Speed = 14f, Radius = 0.12f
            });
            k.RunThrough(20);
            Kit.AreClose(new Vector2(5f, 7f), wolf.Pos);
        }

        // ------------------------------------------------------------ 6.3 선돌지기

        // d ≤ 3.0 휩쓸기(fan r3.0, 110도, 48틱, 피해 2), 3.0 < d ≤ 8 내려찍기(circle r2.5, 플레이어 위치, 66틱, 피해 2)
        [Test]
        public void S05_GOLEM__PicksSweepWithinThreeElseSlamWithinEight()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 9f, 7f);
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Fan, z.Shape);
            Assert.AreEqual(3.0f, z.Radius, 1e-6f);
            Assert.AreEqual(110f, z.FanDegrees, 1e-6f);
            Assert.AreEqual(48, z.TelegraphTicks);
            Assert.AreEqual(2, z.BaseDamage);
            Assert.AreEqual(ZoneAnchor.Owner, z.Anchor);
            Assert.IsTrue(z.AimAtPlayer);
            Assert.AreEqual(PlayerDamageSource.Telegraph, z.DamageSource);
            Assert.AreEqual(1, g.CurrentAttack);

            k = new Kit();
            k.Spawn(Ids.Enemy.GolemMenhir, 8.9f, 7f);
            k.RunThrough(0);
            z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, z.Shape);
            Assert.AreEqual(2.5f, z.Radius, 1e-6f);
            Assert.AreEqual(66, z.TelegraphTicks);
            Assert.AreEqual(2, z.BaseDamage);
            Assert.AreEqual(ZoneAnchor.TargetPlayer, z.Anchor, "중심 = 예고 시작 순간 플레이어 위치");
            Assert.IsTrue(z.Grazeable);

            k = new Kit();
            k.Spawn(Ids.Enemy.GolemMenhir, 4f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(ZoneShape.Circle, k.Threats.Tickets[0].Req.Zones[0].Shape, "d = 8");

            k = new Kit();
            var far = k.Spawn(Ids.Enemy.GolemMenhir, 3.9f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(0, k.Threats.Tickets.Count, "d = 8.1");
            Assert.AreEqual(3.9f + 1.8f / 60f, far.Pos.x, 1e-4f, "이동 1.8칸/초");
        }

        // 내려찍기: 예고 66틱 → 빈틈 72틱(철갑 무효) → 추적. 시선은 예고·판정·빈틈 중 고정.
        [Test]
        public void S05_GOLEM__SlamTimelineAndArmorOffDuringGap()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 8f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(EnemyAiState.WindUp, g.Ai);
            k.RunBefore(20);
            k.P.Pos = new Vector2(8f, 3f);
            k.RunThrough(65);
            Assert.AreEqual(EnemyAiState.WindUp, g.Ai);
            Assert.IsTrue(g.ArmorActive);
            Kit.AreClose(new Vector2(1, 0), g.Facing);
            k.RunThrough(66);
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
            Assert.IsFalse(g.ArmorActive, "빈틈 중 철갑 무효");
            k.RunThrough(137);
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
            Kit.AreClose(new Vector2(1, 0), g.Facing);
            Kit.AreClose(new Vector2(8f, 7f), g.Pos);
            k.RunThrough(138);
            Assert.AreEqual(EnemyAiState.Act, g.Ai);
            Assert.IsTrue(g.ArmorActive);
        }

        // 휩쓸기 빈틈 48틱. 버프 선돌지기 내려찍기 빈틈 72 → 51.
        [Test]
        public void S05_GOLEM__SweepGap48AndBuffedSlamGap51()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 9.5f, 7f);
            k.RunThrough(48 + 47);
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
            k.RunThrough(48 + 48);
            Assert.AreEqual(EnemyAiState.Act, g.Ai);

            k = new Kit();
            g = k.Spawn(Ids.Enemy.GolemMenhir, 8f, 7f);
            GiveBuff(g);
            k.RunThrough(66 + 50);
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
            k.RunThrough(66 + 51);
            Assert.AreEqual(EnemyAiState.Act, g.Ai);
        }

        // 중량: 밀리지·얼지·경직·기절 안 함 → 예고도 끊기지 않는다
        [Test]
        public void S05_GOLEM__HeavyTelegraphNeverInterrupted()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 8f, 7f);
            k.RunThrough(10);
            k.Sys.ApplyCc(k.W, g, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero);
            k.Sys.ApplyCc(k.W, g, CcKind.Stun, 60f, CcSource.ThunderSpear, Vector2.zero);
            Assert.AreEqual(EnemyAiState.WindUp, g.Ai);
            Assert.AreEqual(0, k.Threats.CancelOwnerCalls.Count);
            k.RunThrough(66);
            Assert.AreEqual(EnemyAiState.Recovery, g.Ai);
        }

        // ------------------------------------------------------------ 6.4 갓버섯 술사

        // 시전 24틱(구역 없음) → 3발 동시(가운데 = 플레이어 쪽, 20도 퍼짐, 속도 6.5, 사거리 9, 반지름 0.12) → 빈틈 36틱, 쿨 120틱
        [Test]
        public void S05_SHAMAN__TripleShotTimeline()
        {
            var k = new Kit();
            var s = k.Spawn(Ids.Enemy.ShamanCap, 6f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(EnemyAiState.WindUp, s.Ai);
            Assert.AreEqual(0, k.Threats.Tickets.Count, "시전은 예고 구역이 아니다");
            // lock at_start: 시전 중 플레이어가 움직여도 가운데 탄은 시작 순간의 방향
            k.RunBefore(10);
            k.P.Pos = new Vector2(6f, 1f);
            k.RunThrough(23);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            k.RunThrough(24);
            var shots = k.Threats.ShotsAt(24);
            Assert.AreEqual(3, shots.Count);
            AssertAngles(shots, new[] { 0f, 20f, 340f });
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(6.5f, shots[i].Spec.Speed, 1e-5f);
                Assert.AreEqual(9f, shots[i].Spec.Range, 1e-5f);
                Assert.AreEqual(0.12f, shots[i].Spec.Radius, 1e-6f);
                Assert.AreEqual(1, shots[i].Spec.BaseDamage);
                Assert.IsTrue(shots[i].Spec.Grazeable);
                Assert.AreEqual(shots[0].Spec.CastId, shots[i].Spec.CastId);
                Kit.AreClose(new Vector2(6f, 7f), shots[i].Spec.Pos);
            }
            k.P.Pos = new Vector2(12f, 7f);
            Assert.AreEqual(EnemyAiState.Recovery, s.Ai);
            k.RunThrough(59);
            Assert.AreEqual(EnemyAiState.Recovery, s.Ai);
            k.RunThrough(60);
            Assert.AreEqual(EnemyAiState.Act, s.Ai);
            // 다음 시전은 발사 뒤 120틱(144틱)부터 → 발사 168틱
            k.RunThrough(167);
            Assert.AreEqual(3, k.Threats.Shots.Count);
            k.RunThrough(168);
            Assert.AreEqual(6, k.Threats.Shots.Count);
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

        // 거리 5~8 유지. 사거리 밖(9 초과)이면 쏘지 않고 다가간다.
        [Test]
        public void S05_SHAMAN__KeepsDistanceFiveToEight()
        {
            var k = new Kit();
            var near = k.Spawn(Ids.Enemy.ShamanCap, 8f, 7f);       // d = 4 → 물러난다
            Kit.Brain(near).NextAttackTick = 100000;
            var far = k.Spawn(Ids.Enemy.ShamanCap, 2f, 7f);        // d = 10 → 다가간다(사거리 밖이라 시전 없음)
            var mid = k.Spawn(Ids.Enemy.ShamanCap, 12f, 13f);      // d = 6 → 정지
            Kit.Brain(mid).NextAttackTick = 100000;
            k.RunThrough(0);
            Assert.AreEqual(8f - 2.5f / 60f, near.Pos.x, 1e-4f);
            Assert.AreEqual(2f + 2.5f / 60f, far.Pos.x, 1e-4f);
            Kit.AreClose(new Vector2(12f, 13f), mid.Pos);
            Assert.AreEqual(EnemyAiState.Act, far.Ai);
        }

        // Hp ≤ 50% → 엄폐: 가장 가까운 선돌지기 뒤 1.5칸. 선돌지기(10,7), P(4,7) → (11.5,7)
        [Test]
        public void S05_SHAMAN__CoverBehindNearestGolem()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(4f, 7f);
            var golem = k.Spawn(Ids.Enemy.GolemMenhir, 10f, 7f);
            golem.MoveSpeed = 0f;
            var vine = k.Spawn(Ids.Enemy.VineThorn, 11.5f, 2f);
            var s = k.Spawn(Ids.Enemy.ShamanCap, 11.5f, 4f);
            Kit.Brain(s).NextAttackTick = 100000;
            s.Hp = s.MaxHp * 0.5f;
            k.RunThrough(0);
            Assert.AreEqual(4f + 2.5f / 60f, s.Pos.y, 1e-4f, "선돌지기 쪽 엄폐 자리로(가시덩굴이 더 가까워도 선돌지기 먼저)");
            k.RunThrough(200);
            Kit.AreClose(new Vector2(11.5f, 7f), s.Pos, 0.06f);
            Assert.IsFalse(vine.Dead);
        }

        // 선돌지기가 없으면 가장 가까운 가시덩굴. 가시덩굴(6,3), P(6,9) → (6,1.5)
        [Test]
        public void S05_SHAMAN__CoverBehindVineWhenNoGolem()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(6f, 9f);
            var vine = k.Spawn(Ids.Enemy.VineThorn, 6f, 3f);
            Kit.Brain(vine).NextAttackTick = 100000;
            var s = k.Spawn(Ids.Enemy.ShamanCap, 9f, 1.5f);
            Kit.Brain(s).NextAttackTick = 100000;
            s.Hp = s.MaxHp * 0.4f;
            k.RunThrough(200);
            Kit.AreClose(new Vector2(6f, 1.5f), s.Pos, 0.06f);
        }

        // 둘 다 없으면 Kite: 플레이어 반대로 거리 ≥ 7
        [Test]
        public void S05_SHAMAN__KiteToSevenWhenNoCover()
        {
            var k = new Kit();
            var s = k.Spawn(Ids.Enemy.ShamanCap, 6.5f, 7f);       // d = 5.5: 평소면 정지, 엄폐 없으면 7까지 물러난다
            Kit.Brain(s).NextAttackTick = 100000;
            s.Hp = s.MaxHp * 0.3f;
            k.RunThrough(0);
            Assert.AreEqual(6.5f - 2.5f / 60f, s.Pos.x, 1e-4f);
            k.RunThrough(300);
            Assert.AreEqual(7f, 12f - s.Pos.x, 0.05f);
        }

        // ------------------------------------------------------------ 6.5 꽈리 벌레

        // d ≤ 1.2이면 멈추고 점화: 자기 중심 원 r1.8, 54틱, 피해 1. 터지면 자신 사망, 이 폭발은 유폭을 남기지 않는다.
        [Test]
        public void S05_BUG__IgniteSelfDestructs()
        {
            var k = new Kit();
            var bug = k.Spawn(Ids.Enemy.BugLantern, 10.8f, 7f);
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 10.8f, 8f);
            slime.MoveSpeed = 0f;
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, z.Shape);
            Assert.AreEqual(1.8f, z.Radius, 1e-6f);
            Assert.AreEqual(54, z.TelegraphTicks);
            Assert.AreEqual(1, z.BaseDamage);
            Assert.AreEqual(ZoneAnchor.Owner, z.Anchor);
            k.RunThrough(54);
            Assert.IsFalse(bug.Dead, "판정 틱에는 아직 있다(구역이 터진 다음 틱에 죽는다)");
            Kit.AreClose(new Vector2(10.8f, 7f), bug.Pos);
            k.RunThrough(55);
            Assert.IsTrue(bug.Dead);
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemyKilled, bug.Id));
            k.RunThrough(120);
            Assert.AreEqual(0, k.Spells.Direct.Count, "점화 폭발은 적에게 피해가 없다(U-37)");
        }

        [Test]
        public void S05_BUG__ChasesUntilOnePointTwo()
        {
            var k = new Kit();
            var bug = k.Spawn(Ids.Enemy.BugLantern, 10.7f, 7f);     // d = 1.3
            k.RunThrough(0);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            Assert.AreEqual(10.7f + 3.6f / 60f, bug.Pos.x, 1e-4f);
            k.RunThrough(3);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
        }

        // 유폭: 점화 전에 죽으면 24틱 뒤 같은 반경(1.8)으로 터져 적에게만 20 × H(w) 무속성. 플레이어 0.
        [Test]
        public void S05_BUG__SympatheticBlastAfter24TicksHitsEnemiesOnly()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2f;
            k.P.Pos = new Vector2(5.5f, 5f);
            k.Threats.BlockAll = true;
            var bug = k.Spawn(Ids.Enemy.BugLantern, 5f, 5f);
            var near = k.Spawn(Ids.Enemy.SlimeMoss, 7f, 5f);       // 거리 2.0 < 1.8 + 0.35
            var far = k.Spawn(Ids.Enemy.SlimeMoss, 9f, 5f);
            near.MoveSpeed = 0f;
            far.MoveSpeed = 0f;
            bug.PendingDeath = true;
            k.RunThrough(23);
            Assert.AreEqual(0, k.Spells.Direct.Count);
            int hitsBefore = k.Player.Hits.Count;
            k.RunThrough(24);
            Assert.AreEqual(1, k.Spells.Direct.Count);
            DirectDamage d = k.Spells.Direct[0].Damage;
            Assert.AreEqual(near.Id, d.TargetEnemyId);
            Assert.AreEqual(20f * 2f, d.Amount, 1e-4f);
            Assert.AreEqual(Ids.Element.Neutral, d.Element);
            Assert.AreEqual(DamageOrigin.Environment, d.Origin);
            Assert.AreEqual(-1, d.AttackerSlot);
            Assert.AreEqual(hitsBefore, k.Player.Hits.Count, "플레이어 피해 0");
            Assert.AreEqual(1, k.CountEvents(SimEventType.Explosion));
        }

        // 유폭으로 죽은 꽈리도 유폭(연쇄). 점화 중 사망도 유폭 처리(구역 취소, U-38).
        [Test]
        public void S05_BUG__ChainAndIgnitingDeathAlsoBlast()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2f;
            k.Spells.Apply = true;
            k.P.Pos = new Vector2(5f, 6f);                          // 꽈리 A가 바로 점화한다(d = 1.0)
            var a = k.Spawn(Ids.Enemy.BugLantern, 5f, 5f);
            var b = k.Spawn(Ids.Enemy.BugLantern, 6.5f, 5f);
            b.MoveSpeed = 0f;                                        // B는 플레이어에서 1.8칸(> 1.2)이라 점화하지 않는다
            var c = k.Spawn(Ids.Enemy.SlimeMoss, 8.2f, 5f);        // A에서 3.2(밖), B에서 1.7(안)
            c.MoveSpeed = 0f;
            c.Hp = c.MaxHp = 1000f;
            k.RunThrough(9);
            Assert.AreEqual(EnemyAiState.WindUp, a.Ai);
            a.PendingDeath = true;                                   // 10틱에 점화 중 사망
            k.RunThrough(10);
            CollectionAssert.Contains(k.Threats.CancelOwnerCalls, a.Id);
            Assert.IsTrue(k.Threats.Tickets[0].Cancelled);
            k.RunThrough(34);
            Assert.AreEqual(1, k.Spells.At(34).Count, "A의 유폭이 B만 맞힌다");
            Assert.AreEqual(b.Id, k.Spells.At(34)[0].Damage.TargetEnemyId);
            Assert.IsTrue(b.Dead, "B 체력 36 < 40");
            k.RunThrough(58);
            var second = k.Spells.At(58);
            Assert.AreEqual(1, second.Count, "B의 유폭(24틱 뒤)이 C를 맞힌다");
            Assert.AreEqual(c.Id, second[0].Damage.TargetEnemyId);
        }

        // 빙결 시 점화 취소, 풀리면 처음부터 54틱. 끌리고 밀린다.
        [Test]
        public void S05_BUG__FreezeCancelsIgnitionThenRestartsFull54()
        {
            var k = new Kit();
            var bug = k.Spawn(Ids.Enemy.BugLantern, 10.8f, 7f);
            k.RunThrough(9);
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, bug, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.IsTrue(k.Threats.Tickets[0].Cancelled);
            k.RunThrough(70);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            Assert.AreEqual(70, k.Threats.Tickets[1].RequestTick);
            Assert.AreEqual(54, k.Threats.Tickets[1].Req.Zones[0].TelegraphTicks);
            k.RunThrough(70 + 54);
            Assert.IsFalse(bug.Dead);
            k.RunThrough(70 + 55);
            Assert.IsTrue(bug.Dead);
        }

        // ------------------------------------------------------------ 6.6 솔침 고슴도치

        // s~s+35 추적, s+36 고정, s+60 발사(속도 16, 벽까지), s+60~s+144 빈틈
        [Test]
        public void S05_HOG__SnipeTimeline()
        {
            var k = new Kit();
            var hog = k.Spawn(Ids.Enemy.HogNeedle, 4f, 7f);         // d = 8
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.AimLine, z.Shape);
            Assert.AreEqual(0.4f, z.Width, 1e-6f);
            Assert.LessOrEqual(z.Length, 0f, "전장 끝까지");
            Assert.AreEqual(60, z.TelegraphTicks);
            Assert.AreEqual(24, z.LockLastTicks);
            Assert.AreEqual(0, z.BaseDamage, "피해는 가시(탄)가 준다");
            k.RunThrough(59);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            k.RunThrough(60);
            Assert.AreEqual(1, k.Threats.Shots.Count);
            var shot = k.Threats.Shots[0].Spec;
            Assert.AreEqual(16f, shot.Speed, 1e-5f);
            Assert.AreEqual(0.12f, shot.Radius, 1e-6f);
            Assert.Less(shot.Range, 0f, "벽에 박히면 소멸(사거리 없음)");
            Assert.AreEqual(1, shot.BaseDamage);
            Assert.IsTrue(shot.Grazeable);
            Kit.AreClose(new Vector2(1, 0), shot.Dir);
            Assert.AreEqual(EnemyAiState.Recovery, hog.Ai);
            k.RunThrough(143);
            Assert.AreEqual(EnemyAiState.Recovery, hog.Ai);
            k.RunThrough(144);
            Assert.AreEqual(EnemyAiState.Act, hog.Ai);
            Kit.AreClose(new Vector2(4f, 7f), hog.Pos);
            Assert.AreEqual(0, k.Player.Hits.Count);
        }

        // 마지막 24틱은 방향 고정(R4): 36틱까지 따라가고 그 뒤 플레이어가 움직여도 그대로. 7칸 안으로 와도 조준은 계속(U-42).
        [Test]
        public void S05_HOG__AimTracksUntilLockTickThenFixed()
        {
            var k = new Kit();
            k.Threats.ProvideZones = false;      // 구역을 못 읽을 때의 자체 계산
            k.Spawn(Ids.Enemy.HogNeedle, 4f, 7f);
            k.RunBefore(20);
            k.P.Pos = new Vector2(4f, 2f);        // 위쪽, 거리 5(7칸 안)
            k.RunBefore(40);
            k.P.Pos = new Vector2(12f, 7f);
            k.RunThrough(60);
            Assert.AreEqual(1, k.Threats.Shots.Count);
            Kit.AreClose(new Vector2(0, -1), k.Threats.Shots[0].Spec.Dir);
        }

        // d < 7 뒷걸음(2.2), d > 10 접근, 사이는 정지
        [Test]
        public void S05_HOG__KeepsSevenToTen()
        {
            var k = new Kit();
            var near = k.Spawn(Ids.Enemy.HogNeedle, 6f, 7f);        // d = 6
            var far = k.Spawn(Ids.Enemy.HogNeedle, 1f, 7f);         // d = 11
            k.RunThrough(0);
            Assert.AreEqual(6f - 2.2f / 60f, near.Pos.x, 1e-4f);
            Assert.AreEqual(1f + 2.2f / 60f, far.Pos.x, 1e-4f);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
        }

        // ------------------------------------------------------------ 6.7 화분 족제비

        // 판정 틱 e: e~e+95 지상(꼬리표 없음, 접촉 1), e+96~e+125 파고들기, e+126 땅속. 예고 중엔 아직 땅속이고 구역 중심으로 옮겨 간다.
        [Test]
        public void S05_WEASEL__EruptSurfaceDigTimeline()
        {
            var k = new Kit();
            var wz = k.Spawn(Ids.Enemy.WeaselPot, 10.5f, 7f);        // d = 1.5 ≤ 2
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, z.Shape);
            Assert.AreEqual(1.2f, z.Radius, 1e-6f);
            Assert.AreEqual(42, z.TelegraphTicks);
            Assert.AreEqual(1, z.BaseDamage);
            Assert.AreEqual(ZoneAnchor.TargetPlayer, z.Anchor, "그 순간 플레이어 위치");
            Kit.AreClose(new Vector2(12f, 7f), wz.Pos);             // U-46
            const int e = 42;
            k.RunThrough(e - 1);
            Assert.IsTrue(wz.Burrowed && wz.Has(EnemyTags.Burrowed), "예고 중에도 땅속(U-45)");
            Assert.AreEqual(0f, k.Sys.ModifyIncomingDamage(k.W, wz, 10f, false, false, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0, k.Player.Hits.Count);
            k.RunThrough(e);
            Assert.IsFalse(wz.Burrowed);
            Assert.IsFalse(wz.Has(EnemyTags.Burrowed), "지상: 꼬리표 없음(TV-TAG-02)");
            Assert.AreEqual(e, k.Player.AcceptedHits()[0].Tick, "지상 접촉 1");
            Assert.AreEqual(10f, k.Sys.ModifyIncomingDamage(k.W, wz, 10f, false, false, Vector2.zero, Vector2.zero), 1e-5f);
            k.RunThrough(e + 95);
            Assert.AreEqual(BrainSub.Surface, Kit.Brain(wz).Sub);
            Kit.AreClose(new Vector2(12f, 7f), wz.Pos);             // 지상 이동 0
            k.RunThrough(e + 96);
            Assert.AreEqual(BrainSub.Dig, Kit.Brain(wz).Sub);
            Assert.IsFalse(wz.Burrowed, "파고드는 동안 피해를 받는다(U-49)");
            k.RunThrough(e + 125);
            Assert.IsFalse(wz.Burrowed);
            k.RunThrough(e + 126);
            Assert.IsTrue(wz.Burrowed && wz.Has(EnemyTags.Burrowed));
            Assert.AreEqual(EnemyAiState.Act, wz.Ai);
        }

        // 땅속 이동 3.5. 땅속 240틱이 지나면 자기 위치에 솟는다.
        [Test]
        public void S05_WEASEL__EruptsAtOwnPositionAfter240TicksBurrowed()
        {
            var k = new Kit();
            var mover = k.Spawn(Ids.Enemy.WeaselPot, 2f, 3f);
            k.RunThrough(0);
            Assert.AreEqual(3.5f / 60f, (mover.Pos - new Vector2(2f, 3f)).magnitude, 1e-4f);

            k = new Kit();
            var wz = k.Spawn(Ids.Enemy.WeaselPot, 2f, 7f);
            wz.MoveSpeed = 0f;
            k.RunThrough(239);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            k.RunThrough(240);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneAnchor.Owner, z.Anchor);
            Assert.IsFalse(z.AimAtPlayer);
            Kit.AreClose(new Vector2(2f, 7f), wz.Pos);
        }

        // 대기 줄에 걸리면 땅속에서 대기. 요청 뒤 180틱 더 막히면 목표를 자기 위치로 바꿔 다시 청한다(U-50).
        [Test]
        public void S05_WEASEL__RetargetsSelfAfter180TicksBlocked()
        {
            var k = new Kit();
            k.Threats.BlockAll = true;
            var wz = k.Spawn(Ids.Enemy.WeaselPot, 10.5f, 7f);
            k.RunThrough(179);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            Assert.AreEqual(EnemyAiState.Waiting, wz.Ai);
            Assert.IsTrue(wz.Burrowed);
            Kit.AreClose(new Vector2(10.5f, 7f), wz.Pos);
            k.RunThrough(180);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            CollectionAssert.Contains(k.Threats.CancelTicketCalls, k.Threats.Tickets[0].Id);
            Assert.AreEqual(ZoneAnchor.Owner, k.Threats.Tickets[1].Req.Zones[0].Anchor);
            Assert.AreEqual(180, k.Threats.Tickets[1].RequestTick);
        }

        // ------------------------------------------------------------ 6.8 해거름 박쥐

        // 물결 비행: 실제 이동량은 3.6칸/초로 자른다(R2 우선). 위상은 난수 0~71틱. 플레이어에게 다가간다.
        [Test]
        public void S05_BAT__WaveFlightNeverExceedsMoveSpeed()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20f, 7f);
            var bat = k.Spawn(Ids.Enemy.BatDusk, 2f, 7f);
            int phase = Kit.Brain(bat).WavePhase;
            Assert.IsTrue(phase >= 0 && phase < 72);
            float maxDev = 0f;
            Vector2 prev = bat.Pos;
            for (int i = 0; i < 600; i++)
            {
                k.W.Step();
                Assert.LessOrEqual((bat.Pos - prev).magnitude, 3.6f / 60f + 1e-4f);
                prev = bat.Pos;
                maxDev = Mathf.Max(maxDev, Mathf.Abs(bat.Pos.y - 7f));
            }
            Assert.Greater(maxDev, 0.3f, "좌우로 물결친다");
            Assert.LessOrEqual(maxDev, 1.0f + 1e-3f, "진폭 1칸");
            Assert.Less((k.P.Pos - bat.Pos).magnitude, 12f, "플레이어 쪽으로 나아간다");
            Assert.AreEqual(0, k.Threats.Tickets.Count, "예고 없음");
        }

        [Test]
        public void S05_BAT__BuffedCapThreePointSeven()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20f, 7f);
            var bat = k.Spawn(Ids.Enemy.BatDusk, 2f, 7f);
            GiveBuff(bat);
            Vector2 prev = bat.Pos;
            float max = 0f;
            for (int i = 0; i < 200; i++)
            {
                k.W.Step();
                max = Mathf.Max(max, (bat.Pos - prev).magnitude);
                prev = bat.Pos;
            }
            Assert.LessOrEqual(max, 3.7f / 60f + 1e-4f);
            Assert.Greater(max, 3.6f / 60f + 1e-4f);
        }

        // ------------------------------------------------------------ 6.9 도깨비불

        // 시전 36틱 → 8방향(45도, 첫 탄이 플레이어 쪽, 속도 4.5, 사거리 9) → 빈틈 60틱, 쿨 150틱
        [Test]
        public void S05_WISP__RingTimeline()
        {
            var k = new Kit();
            var wisp = k.Spawn(Ids.Enemy.WispStray, 6f, 7f);
            k.RunThrough(35);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            Assert.AreEqual(0, k.Threats.Tickets.Count, "시전은 예고 구역이 아니다");
            k.RunThrough(36);
            var shots = k.Threats.ShotsAt(36);
            Assert.AreEqual(8, shots.Count);
            Kit.AreClose(new Vector2(1, 0), shots[0].Spec.Dir);
            AssertAngles(shots, new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f });
            Assert.AreEqual(4.5f, shots[0].Spec.Speed, 1e-5f);
            Assert.AreEqual(9f, shots[0].Spec.Range, 1e-5f);
            Assert.AreEqual(0.12f, shots[0].Spec.Radius, 1e-6f);
            k.RunThrough(95);
            Assert.AreEqual(EnemyAiState.Recovery, wisp.Ai);
            k.RunThrough(96);
            Assert.AreEqual(EnemyAiState.Act, wisp.Ai);
            k.RunThrough(36 + 150 + 35);
            Assert.AreEqual(8, k.Threats.Shots.Count);
            k.RunThrough(36 + 150 + 36);
            Assert.AreEqual(16, k.Threats.Shots.Count);
        }

        // 깜박: 300틱마다. 사라지기 18틱 → 플레이어에서 5~7칸 빈자리(벽 1칸 이상) → 나타나기 18틱 → 30틱 시전 금지
        [Test]
        public void S05_WISP__BlinkEveryFiveSeconds()
        {
            var k = new Kit();
            var wisp = k.Spawn(Ids.Enemy.WispStray, 6f, 7f);
            k.RunThrough(299);
            Kit.AreClose(new Vector2(6f, 7f), wisp.Pos);            // 거리 6: 5~7 유지라 제자리
            k.RunThrough(300);
            Assert.AreEqual(BrainSub.Vanish, Kit.Brain(wisp).Sub);
            k.RunThrough(317);
            Kit.AreClose(new Vector2(6f, 7f), wisp.Pos);
            // 시전이 준비돼 있어도 나타난 뒤 0.5초는 못 쏜다
            Kit.Brain(wisp).NextAttackTick = 0;
            k.RunThrough(318);
            Assert.AreEqual(BrainSub.Appear, Kit.Brain(wisp).Sub);
            float d = (wisp.Pos - k.P.Pos).magnitude;
            Assert.IsTrue(d >= 5f - 1e-3f && d <= 7f + 1e-3f, "거리 " + d);
            Assert.IsTrue(k.W.InArena(wisp.Pos, 1f), "벽에서 1칸 이상");
            Assert.Greater((wisp.Pos - new Vector2(6f, 7f)).magnitude, 0.1f);
            Vector2 at = wisp.Pos;
            int shotsBefore = k.Threats.Shots.Count;
            k.RunThrough(336 + 29);
            Assert.AreNotEqual(EnemyAiState.WindUp, wisp.Ai);
            k.RunThrough(336 + 30);
            Assert.AreEqual(EnemyAiState.WindUp, wisp.Ai, "나타난 뒤 30틱이 지나야 시전");
            k.RunThrough(336 + 30 + 36);
            Assert.AreEqual(shotsBefore + 8, k.Threats.Shots.Count);
            Assert.AreEqual(600, Kit.Brain(wisp).NextBlinkTick);
            Assert.LessOrEqual((wisp.Pos - at).magnitude, 2.0f / 60f * 31f + 1e-3f, "떠다니기 2.0");
        }

        // 깜박 자리는 다른 몸과 겹치지 않는다
        [Test]
        public void S05_WISP__BlinkAvoidsOtherBodies()
        {
            var k = new Kit(3);
            var wisp = k.Spawn(Ids.Enemy.WispStray, 6f, 7f);
            var others = new List<EnemyState>();
            for (int i = 0; i < 12; i++)
            {
                var o = k.Spawn(Ids.Enemy.VineThorn, 12f + 6f * Mathf.Cos(i * Mathf.PI / 6f), 7f + 5.5f * Mathf.Sin(i * Mathf.PI / 6f));
                Kit.Brain(o).NextAttackTick = 100000;
                others.Add(o);
            }
            k.RunThrough(318);
            for (int i = 0; i < others.Count; i++)
                Assert.GreaterOrEqual((wisp.Pos - others[i].Pos).magnitude, wisp.BodyRadius + others[i].BodyRadius - 1e-4f);
        }

        // 평소 거리 5~7칸 유지, 떠다니기 2.0
        [Test]
        public void S05_WISP__KeepsFiveToSeven()
        {
            var k = new Kit();
            var near = k.Spawn(Ids.Enemy.WispStray, 8f, 7f);        // d = 4
            Kit.Brain(near).NextAttackTick = 100000;
            var far = k.Spawn(Ids.Enemy.WispStray, 2f, 7f);         // d = 10(사거리 9 밖)
            k.RunThrough(0);
            Assert.AreEqual(8f - 2.0f / 60f, near.Pos.x, 1e-4f);
            Assert.AreEqual(2f + 2.0f / 60f, far.Pos.x, 1e-4f);
        }

        // ------------------------------------------------------------ 6.10 가시덩굴

        // 흙 금 60틱(피해 주고받지 않음) → 씨앗: 플레이어 위치 원 r1.5, 60틱, 피해 1, 가시밭(r1.5, 4초, 동시 3개), 간격 210틱(허가 틱 기준). 이동 없음.
        [Test]
        public void S05_VINE__EmergeSeedIntervalAndThornField()
        {
            var k = new Kit();
            var vine = k.Spawn(Ids.Enemy.VineThorn, 5f, 5f, instant: false);
            SimEvent ev;
            Assert.IsTrue(k.TryFindEvent(SimEventType.EnemySpawnNotice, 0, out ev));
            Assert.AreEqual(60, ev.A);
            k.RunThrough(59);
            Assert.AreEqual(EnemyAiState.Spawning, vine.Ai);
            Assert.AreEqual(0f, k.Sys.ModifyIncomingDamage(k.W, vine, 10f, false, false, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            k.RunThrough(60);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Circle, z.Shape);
            Assert.AreEqual(1.5f, z.Radius, 1e-6f);
            Assert.AreEqual(60, z.TelegraphTicks);
            Assert.AreEqual(1, z.BaseDamage);
            Assert.AreEqual(ZoneAnchor.TargetPlayer, z.Anchor);
            Assert.IsNotNull(z.LeavesHazard);
            Assert.AreEqual("thorn_field", z.LeavesHazard.Kind);
            Assert.AreEqual(1.5f, z.LeavesHazard.Radius, 1e-6f);
            Assert.AreEqual(4f, z.LeavesHazard.DurationSeconds, 1e-6f);
            Assert.AreEqual(3, z.LeavesHazard.MaxCount);
            Assert.AreEqual(1.5f, z.LeavesHazard.ContactCooldownSeconds, 1e-6f);
            k.RunThrough(60 + 209);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(60 + 210);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            k.RunThrough(60 + 420);
            Assert.AreEqual(3, k.Threats.Tickets.Count);
            Kit.AreClose(new Vector2(5f, 5f), vine.Pos);
        }

        // 간격은 허가 틱 기준(U-59): 20틱 기다렸다 허가되면 다음 씨앗은 허가 틱 + 210
        [Test]
        public void S05_VINE__IntervalCountsFromGrantTick()
        {
            var k = new Kit();
            k.Threats.BlockUntilTick = 20;
            k.Spawn(Ids.Enemy.VineThorn, 5f, 5f);
            k.RunThrough(20 + 209);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(20 + 210);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
        }

        // 뿌리: 빙결되면 예고 취소
        [Test]
        public void S05_VINE__RootedFreezeCancelsTelegraph()
        {
            var k = new Kit();
            var vine = k.Spawn(Ids.Enemy.VineThorn, 5f, 5f);
            k.RunThrough(10);
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, vine, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.IsTrue(k.Threats.Tickets[0].Cancelled);
        }

        // ------------------------------------------------------------ 6.11 엄니 멧돼지

        // 띠 폭 1.6, 벽까지, 예고 54틱(시작 순간 방향 고정) → 16칸/초 돌진(피해 2) → 전장 벽 충돌 → 기절 120틱, 기절 중 받는 피해 ×1.5. 쿨 180틱.
        [Test]
        public void S05_BOAR__ChargeToWallThenStun()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f);
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.AreEqual(1.6f, z.Width, 1e-6f);
            Assert.LessOrEqual(z.Length, 0f, "벽까지");
            Assert.AreEqual(54, z.TelegraphTicks);
            Assert.AreEqual(0, z.LockLastTicks);
            k.RunThrough(53);
            Assert.IsFalse(boar.DashActive);
            k.RunThrough(54);
            Assert.IsTrue(boar.DashActive);
            Assert.AreEqual(2, boar.DashBaseDamage);
            Assert.AreEqual(4f + 16f / 60f, boar.Pos.x, 1e-4f);
            // 벽(x = 24 − 0.55)까지 19.45칸 → 73틱째 이동(126틱)에 닿는다
            k.RunThrough(125);
            Assert.IsTrue(boar.DashActive);
            k.RunThrough(126);
            Assert.IsFalse(boar.DashActive);
            Assert.AreEqual(k.W.ArenaWidth - boar.BodyRadius, boar.Pos.x, 1e-3f);
            Assert.AreEqual(126 + 120, boar.StunUntilTick);
            Assert.AreEqual(1.5f, boar.DamageTakenMul, 1e-6f);
            Assert.AreEqual(18.75f, k.Sys.ModifyIncomingDamage(k.W, boar, 12.5f, false, false, new Vector2(20, 7), new Vector2(20, 7)), 1e-4f);
            var hits = k.Player.AcceptedHits();
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(PlayerDamageSource.Dash, hits[0].Hit.Source);
            Assert.AreEqual(2, hits[0].Hit.DamageCells);
            k.RunThrough(126 + 119);
            Assert.AreEqual(EnemyAiState.Recovery, boar.Ai);
            Assert.AreEqual(1.5f, boar.DamageTakenMul, 1e-6f);
            Assert.AreEqual(54 + 180, Kit.Brain(boar).NextAttackTick);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(126 + 120);
            Assert.AreNotEqual(EnemyAiState.Recovery, boar.Ai);
            Assert.AreEqual(1f, boar.DamageTakenMul, 1e-6f);
            // 재사용 대기(돌진 시작 + 180틱)는 기절 중에 지났다 → 풀린 틱에 다시 준비한다
            Assert.AreEqual(2, k.Threats.Tickets.Count);
            Assert.AreEqual(126 + 120, k.Threats.Tickets[1].RequestTick);
        }

        // Q-25: 벽 충돌 기절 2초는 두꺼비 버프로 줄어든다 = ceil(120 × 0.7) = 84틱. 받는 피해 ×1.5는 유지.
        [Test]
        public void S10_Q25__BuffedBoarWallStun84Ticks()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f);
            GiveBuff(boar);
            k.RunThrough(126);
            Assert.AreEqual(126 + 84, boar.StunUntilTick);
            Assert.AreEqual(1.5f, boar.DamageTakenMul, 1e-6f);
        }

        // 플레이어와 직선이 막혀 있으면(장애물) 준비하지 않고 걸어간다
        [Test]
        public void S05_BOAR__NoChargeWhileLineBlocked()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10f, 7f);
            k.Threats.ObstacleHit = (a, b) => true;
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f);
            k.RunThrough(9);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            Assert.AreEqual(4f + 10f * 2.4f / 60f, boar.Pos.x, 1e-3f);
            k.Threats.ObstacleHit = null;
            k.RunThrough(10);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
        }

        // 경로의 다른 적: 처음 겹친 틱에 1회 10 × H(w) 무속성, 띠 밖으로 수직 밀어냄(중량은 피해만). 꽈리는 그 자리에서 유폭.
        [Test]
        public void S05_BOAR__PathDamageOncePushesAndTriggersBug()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2f;
            k.P.Pos = new Vector2(10f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f);
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 7.5f);
            var golem = k.Spawn(Ids.Enemy.GolemMenhir, 14f, 7.9f);
            var bug = k.Spawn(Ids.Enemy.BugLantern, 17f, 7.3f);
            var outside = k.Spawn(Ids.Enemy.SlimeMoss, 12f, 9f);     // 띠 밖(0.8 + 0.35 < 2)
            slime.MoveSpeed = 0f; golem.MoveSpeed = 0f; bug.MoveSpeed = 0f; outside.MoveSpeed = 0f;
            Kit.Brain(golem).NextAttackTick = 100000;
            k.RunThrough(126);
            int slimeHits = 0, golemHits = 0, bugHits = 0, outsideHits = 0;
            for (int i = 0; i < k.Spells.Direct.Count; i++)
            {
                DirectDamage d = k.Spells.Direct[i].Damage;
                if (d.TargetEnemyId == slime.Id) { slimeHits++; Assert.AreEqual(20f, d.Amount, 1e-4f); Assert.IsTrue(d.IgnoresArmor); Assert.AreEqual(Ids.Element.Neutral, d.Element); }
                if (d.TargetEnemyId == golem.Id) golemHits++;
                if (d.TargetEnemyId == bug.Id) bugHits++;
                if (d.TargetEnemyId == outside.Id) outsideHits++;
            }
            Assert.AreEqual(1, slimeHits, "적마다 1회(U-62)");
            Assert.AreEqual(1, golemHits);
            Assert.AreEqual(0, bugHits);
            Assert.AreEqual(0, outsideHits);
            Kit.AreClose(new Vector2(8f, 7f + 0.8f + 0.35f), slime.Pos);
            Kit.AreClose(new Vector2(14f, 7.9f), golem.Pos);
            Assert.IsTrue(bug.Dead, "꽈리는 그 자리에서 유폭");
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemyKilled, bug.Id));
            SimEvent killed, blast;
            Assert.IsTrue(k.TryFindEvent(SimEventType.EnemyKilled, bug.Id, out killed));
            k.RunThrough(126 + 30);
            Assert.AreEqual(1, k.CountEvents(SimEventType.Explosion));
            Assert.IsTrue(k.TryFindEvent(SimEventType.Explosion, 0, out blast));
            Assert.AreEqual(killed.Tick + 24, blast.Tick, "죽은 틱에서 24틱 뒤 유폭");
            Kit.AreClose(new Vector2(17f, 7.3f), blast.Pos);
            Assert.IsFalse(boar.Dead);
        }

        // ② 반동 사격으로 취소되지 않고(중량), 얼음 병풍은 부수고 계속 달린다(기절 없음)
        [Test]
        public void S05_BOAR__BreaksIceScreenAndKeepsRunning()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10f, 7f);
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 4f, 7f);
            k.Arts.Hits = (a, b) => a.x < 8f && b.x >= 8f;
            k.Arts.Breaks = true;
            k.RunThrough(90);
            Assert.AreEqual(1, k.Arts.Touched.Count);
            Assert.IsTrue(k.Arts.Touched[0].Dash);
            Assert.IsTrue(boar.DashActive);
            Assert.Greater(boar.Pos.x, 8f);
            Assert.AreEqual(0, boar.StunUntilTick);
        }

        // ------------------------------------------------------------ 6.12 북두꺼비

        // 360틱 주기, 120틱 운다. 끝나면 반경 4 안의 다른 적(보스 제외)에 240틱 버프. 다시 받으면 만료만 갱신.
        [Test]
        public void S05_TOAD__CroakBuffsOthersWithinFour()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20f, 12f);
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f);
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 5f);            // 3칸
            var b = k.Spawn(Ids.Enemy.SlimeMoss, 9.5f, 5f);          // 4.5칸
            var boss = k.AddBossBody(5f, 7f);
            toad.MoveSpeed = 0f; a.MoveSpeed = 0f; b.MoveSpeed = 0f;
            k.RunThrough(359);
            Assert.AreEqual(EnemyAiState.Act, toad.Ai);
            k.RunThrough(360);
            Assert.AreEqual(BrainSub.Croak, Kit.Brain(toad).Sub);
            k.RunThrough(479);
            Assert.IsFalse(a.DrumBuffed);
            k.RunThrough(480);
            Assert.IsTrue(a.DrumBuffed);
            Assert.IsFalse(b.DrumBuffed, "반경 밖");
            Assert.IsFalse(boss.DrumBuffed, "보스 제외");
            Assert.IsFalse(toad.DrumBuffed, "다른 적만");
            Assert.AreEqual(480 + 240, Kit.Brain(a).BuffUntilTick);
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemyBuff, a.Id));
            k.RunThrough(719);
            Assert.IsTrue(a.DrumBuffed);
            k.RunThrough(720);
            Assert.IsFalse(a.DrumBuffed);
            Assert.AreEqual(BrainSub.Croak, Kit.Brain(toad).Sub, "다음 울음은 시작 기준 360틱 뒤");
            k.RunThrough(840);
            Assert.IsTrue(a.DrumBuffed);
            Assert.AreEqual(840 + 240, Kit.Brain(a).BuffUntilTick);
        }

        // 울음 끊기: 우는 동안 누적 피해 ≥ 30 × H(w) → 취소 + 경직 60틱, 버프 없음. 주기는 유지(U-65).
        [TestCase(29.9f, false)]
        [TestCase(30f, true)]
        public void S05_TOAD__CroakBreaksAtThirtyTimesH(float damagePerH, bool broken)
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2f;
            k.P.Pos = new Vector2(20f, 12f);
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f);
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 5f);
            toad.MoveSpeed = 0f; a.MoveSpeed = 0f;
            k.RunThrough(399);
            // 두 번에 나눠 맞아도 쌓인다
            k.Sys.ModifyIncomingDamage(k.W, toad, damagePerH, false, false, Vector2.zero, Vector2.zero);
            k.Sys.ModifyIncomingDamage(k.W, toad, damagePerH, false, false, Vector2.zero, Vector2.zero);
            k.RunThrough(400);
            if (broken)
            {
                Assert.AreEqual(400 + 60, toad.StaggerUntilTick);
                Assert.AreEqual(BrainSub.None, Kit.Brain(toad).Sub);
            }
            else Assert.AreEqual(BrainSub.Croak, Kit.Brain(toad).Sub);
            k.RunThrough(480);
            Assert.AreEqual(!broken, a.DrumBuffed);
            Assert.AreEqual(720, Kit.Brain(toad).NextCroakTick);
        }

        // 빙결·기절로도 취소
        [Test]
        public void S05_TOAD__FreezeCancelsCroak()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20f, 12f);
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f);
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 5f);
            toad.MoveSpeed = 0f; a.MoveSpeed = 0f;
            k.RunThrough(400);
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, toad, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(BrainSub.None, Kit.Brain(toad).Sub);
            k.RunThrough(600);
            Assert.IsFalse(a.DrumBuffed);
        }

        // 혀: d ≤ 3, 띠 폭 0.8 × 3.5, 예고 36틱, 피해 1, 빈틈 60틱, 쿨 90틱
        [Test]
        public void S05_TOAD__TongueTimeline()
        {
            var k = new Kit();
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 9.5f, 7f);
            toad.MoveSpeed = 0f;
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(ZoneShape.Band, z.Shape);
            Assert.AreEqual(0.8f, z.Width, 1e-6f);
            Assert.AreEqual(3.5f, z.Length, 1e-6f);
            Assert.AreEqual(36, z.TelegraphTicks);
            Assert.AreEqual(1, z.BaseDamage);
            Assert.AreEqual(PlayerDamageSource.Telegraph, z.DamageSource);
            k.RunThrough(36);
            Assert.AreEqual(EnemyAiState.Recovery, toad.Ai);
            k.RunThrough(95);
            Assert.AreEqual(EnemyAiState.Recovery, toad.Ai);
            k.RunThrough(96);
            Assert.AreEqual(EnemyAiState.Act, toad.Ai);
            k.RunThrough(36 + 89);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            k.RunThrough(36 + 90);
            Assert.AreEqual(2, k.Threats.Tickets.Count);
        }

        [Test]
        public void S05_TOAD__NoTongueBeyondThree()
        {
            var k = new Kit();
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 8.9f, 7f);        // d = 3.1
            toad.MoveSpeed = 0f;
            k.RunThrough(5);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
        }

        // 이동: 반경 8 안 다른 적 평균 + normalize(무리중심 − P) × 2. 다른 적이 없으면 플레이어 접근.
        [Test]
        public void S05_TOAD__FollowsPackCentreAwayFromPlayer()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(2f, 6f);
            var toad = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f);
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 5f);
            var b = k.Spawn(Ids.Enemy.SlimeMoss, 8f, 7f);
            var farAway = k.Spawn(Ids.Enemy.SlimeMoss, 20f, 12f);    // 반경 8 밖
            a.MoveSpeed = 0f; b.MoveSpeed = 0f; farAway.MoveSpeed = 0f;
            k.RunThrough(0);
            // 무리 중심 (8, 6), 플레이어 반대쪽 2칸 → (10, 6)
            Vector2 step = (new Vector2(10f, 6f) - new Vector2(5f, 5f)).normalized * (2.0f / 60f);
            Kit.AreClose(new Vector2(5f, 5f) + step, toad.Pos, 1e-4f);

            k = new Kit();
            k.P.Pos = new Vector2(12f, 5f);
            var lone = k.Spawn(Ids.Enemy.ToadDrum, 5f, 5f);
            k.RunThrough(0);
            Assert.AreEqual(5f + 2.0f / 60f, lone.Pos.x, 1e-4f);
        }

        // ------------------------------------------------------------ 5절: 얼음 병풍(이동)

        // 일반: 못 지나감 / 비행: 넘어감 / 중량: 부숨
        [Test]
        public void S05_SCREEN__WalkerBlockedFlyerPassesHeavyBreaks()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(14f, 7f);
            k.Arts.Hits = (a, b) => a.x < 10f && b.x >= 10f;
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 9f, 7f);
            var bat = k.Spawn(Ids.Enemy.BatDusk, 9f, 7f);
            k.RunThrough(150);
            Assert.Less(slime.Pos.x, 10f);
            Assert.Greater(bat.Pos.x, 10f);
            Assert.Greater(k.Arts.Touched.Count, 0);
            for (int i = 0; i < k.Arts.Touched.Count; i++)
            {
                Assert.AreEqual(slime.Id, k.Arts.Touched[i].EnemyId, "비행은 닿지 않는다");
                Assert.IsFalse(k.Arts.Touched[i].Dash);
            }

            k = new Kit();
            k.P.Pos = new Vector2(14f, 7f);
            k.Arts.Hits = (a, b) => a.x < 10f && b.x >= 10f;
            k.Arts.Breaks = true;                                    // Arts가 중량이면 부순다고 답한다
            var golem = k.Spawn(Ids.Enemy.GolemMenhir, 9f, 7f);
            Kit.Brain(golem).NextAttackTick = 100000;
            k.RunThrough(60);
            Assert.AreEqual(1, k.Arts.Touched.Count);
            Assert.Greater(golem.Pos.x, 10f);
        }

        // 돌진이 설치 벽에 닿으면: 꼬리표 없음(늑대) → 돌진이 끝난다(벽 파괴와 경직 90틱은 Arts가 ApplyCc로)
        [Test]
        public void S05_SCREEN__WolfDashEndsAtIceScreen()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.Arts.Hits = (a, b) => a.x < 7f && b.x >= 7f;
            k.Arts.Breaks = true;
            k.RunThrough(49);
            Assert.AreEqual(1, k.Arts.Touched.Count);
            Assert.IsTrue(k.Arts.Touched[0].Dash);
            Assert.IsFalse(wolf.DashActive);
            Assert.Less(wolf.Pos.x, 7f);
            Assert.AreEqual(0, k.Player.Hits.Count, "플레이어(9.5)까지 못 갔다");
            Assert.AreEqual(EnemyAiState.Recovery, wolf.Ai);
        }

        // 달리던 돌진에 경직이 걸리면(병풍 충돌의 Arts → ApplyCc) 돌진이 끊긴다
        [Test]
        public void S05_SCREEN__StaggerDuringDashEndsDash()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(32);
            Assert.IsTrue(wolf.DashActive);
            Assert.AreEqual(90f, k.Sys.ApplyCc(k.W, wolf, CcKind.Stagger, 90f, CcSource.IceScreen, Vector2.zero));
            Assert.IsFalse(wolf.DashActive);
            Vector2 at = wolf.Pos;
            k.RunThrough(33 + 89);
            Kit.AreClose(at, wolf.Pos);
            Assert.AreEqual(0, k.Player.Hits.Count);
        }
    }
}
