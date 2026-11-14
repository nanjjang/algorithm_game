using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>실행과 투사체 동작 벡터. 03 요약서 6~8절·15절(S02 16.5~16.7).</summary>
    public class SpellProjectileTests
    {
        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        static float Deg(ProjectileEntity pr) => Dir8Util.SimVectorToDegrees(pr.Dir);

        static void AssertAngle(float expected, float actual)
        {
            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, actual), 1e-2f, "각도 " + expected + " 기대, 실제 " + actual);
        }

        float Damage(EnemyState e) => e.MaxHp - e.Hp;

        // ------------------------------------------------------------ 실행 (6절)

        [Test]
        public void S02_F01__SplitTwoFanMinusPlus10()
        {
            _k.Fire(_k.Compile(B.Mb, B.S2, B.None, B.None, B.None), 0f);
            Assert.AreEqual(2, _k.W.Projectiles.Count);
            AssertAngle(-10f, Deg(_k.W.Projectiles[0]));
            AssertAngle(10f, Deg(_k.W.Projectiles[1]));
        }

        [Test]
        public void S02_F02__SplitThreeFanAscending()
        {
            _k.Fire(_k.Compile(B.Mb, B.S3, B.None, B.None, B.None), 90f);
            Assert.AreEqual(3, _k.W.Projectiles.Count);
            AssertAngle(75f, Deg(_k.W.Projectiles[0]));
            AssertAngle(90f, Deg(_k.W.Projectiles[1]));
            AssertAngle(105f, Deg(_k.W.Projectiles[2]));
        }

        [Test]
        public void S02_F03__MirrorImageFiresBackwardWithSameShotId()
        {
            _k.P.Derived.MirrorImage = true;
            var r = _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None), 0f);
            Assert.AreEqual(2, r.Spawned);
            AssertAngle(0f, Deg(_k.W.Projectiles[0]));
            AssertAngle(180f, Deg(_k.W.Projectiles[1]));
            Assert.AreEqual(_k.W.Projectiles[0].ShotId, _k.W.Projectiles[1].ShotId);
        }

        [Test]
        public void S02_Execute__RootOrderIsSlotThenFanThenMirrorSet()
        {
            _k.P.Derived.MirrorImage = true;
            _k.Fire(_k.Compile(B.Mb, B.Ice, B.S2, B.None, B.None), 0f);
            var list = _k.W.Projectiles;
            Assert.AreEqual(8, list.Count);
            string[] blocks = { B.Mb, B.Mb, B.Ice, B.Ice, B.Mb, B.Mb, B.Ice, B.Ice };
            float[] degs = { -10f, 10f, -10f, 10f, 170f, 190f, 170f, 190f };
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(blocks[i], list[i].Block.Id, "순서 " + i);
                AssertAngle(degs[i], Deg(list[i]));
                Assert.Less(i == 0 ? 0 : list[i - 1].Id, list[i].Id);
            }
        }

        [Test]
        public void S02_Execute__InvalidTreeDoesNothing()
        {
            var r = _k.Fire(_k.Compile(null, B.None, B.None, B.None, B.None));
            Assert.AreEqual(0, r.Spawned);
            Assert.AreEqual(0, r.HeatToAddHu);
            Assert.AreEqual(0, _k.W.Projectiles.Count);
        }

        [Test]
        public void S02_F08__TwistedBarrelRollsOncePerRootShot()
        {
            _k.P.Derived.TwistedBarrel = true;
            _k.Traits.Spreads.Enqueue(20f);
            _k.Traits.Spreads.Enqueue(-25f);
            _k.Traits.Spreads.Enqueue(5f);
            _k.Fire(_k.Compile(B.Mb, B.S3, B.None, B.None, B.None), 0f);
            Assert.AreEqual(3, _k.Traits.SpreadRolls);
            AssertAngle(-15f + 20f, Deg(_k.W.Projectiles[0]));
            AssertAngle(0f - 25f, Deg(_k.W.Projectiles[1]));
            AssertAngle(15f + 5f, Deg(_k.W.Projectiles[2]));

            // NoSpread면 굴리지 않는다.
            var req = _k.Request(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            req.NoSpread = true;
            _k.Sys.Execute(_k.W, req);
            Assert.AreEqual(3, _k.Traits.SpreadRolls);
        }

        [Test]
        public void S02_E11__ArtBudgetCutsFromTheEndWithoutCompensation()
        {
            var req = _k.Request(_k.Compile(B.Mb, B.Ice, B.S3, B.None, B.None));
            req.ProjectileBudget = 5;
            var r = _k.Sys.Execute(_k.W, req);
            Assert.AreEqual(6, r.Requested);
            Assert.AreEqual(5, r.Spawned);
            Assert.AreEqual(5, _k.W.Projectiles.Count);
            foreach (var pr in _k.W.Projectiles) Assert.AreEqual(1f, pr.DamageMul, 1e-5f);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.ShotCapHit));
        }

        // TV-E07: 290발 살아 있고 21발 요청 → 10발 생성, 보전 2.1 (TV-H27: 마력탄이면 피해 21)
        [Test]
        public void S02_TV_E07__ShotCapCompensation()
        {
            for (int i = 0; i < 290; i++)
                _k.W.Projectiles.Add(new ProjectileEntity { Id = _k.W.NewEntityId(), State = ProjectileState.Dead, OwnerSlot = 0 });
            // 갈피 + 확장 3 (L = 8): 마력탄 7개 × 3갈래 = 21발
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.S3, B.None);
            Assert.AreEqual(21, s.InstancesTotal);
            // 살아 있는 탄으로 세도록 Dead 표시는 끄고 상태만 Dead로 둔다(다음 Step에 정리된다).
            var r = _k.Fire(s);
            Assert.AreEqual(21, r.Requested);
            Assert.AreEqual(10, r.Spawned);
            Assert.AreEqual(300, _k.W.Projectiles.Count);

            var cap = _k.EventsOf(SimEventType.ShotCapHit);
            Assert.AreEqual(1, cap.Count);
            Assert.AreEqual(21, cap[0].A);
            Assert.AreEqual(10, cap[0].B);
            Assert.AreEqual(0, cap[0].Slot);
            for (int i = 290; i < 300; i++) Assert.AreEqual(2.1f, _k.W.Projectiles[i].DamageMul, 1e-4f);

            // TV-H27: 보전 2.1인 마력탄이 맞으면 21
            var e = _k.AddEnemy(2.5f, 7f);
            _k.Step(2);
            Assert.AreEqual(21f, Damage(e) / Mathf.Max(1, _k.CountEvents(SimEventType.EnemyDamaged, e.Id)), 1e-3f);
        }

        [Test]
        public void S02_E07b__FullScreenSpawnsNothing()
        {
            for (int i = 0; i < 300; i++)
                _k.W.Projectiles.Add(new ProjectileEntity { Id = _k.W.NewEntityId(), State = ProjectileState.Dead, OwnerSlot = 0 });
            var r = _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            Assert.AreEqual(0, r.Spawned);
            Assert.AreEqual(300, _k.W.Projectiles.Count);
            // 열은 맞혔는지·나갔는지와 무관하게 더한다(HT-4).
            Assert.AreEqual(SimConst.HeatToHu(4f), r.HeatToAddHu);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.ShotCapHit));
        }

        // ------------------------------------------------------------ 투사체 동작 (7절)

        // TV-G01: 마력탄, 적 없음 → 이동 거리가 10 이상이 되는 43틱째에 소멸
        [Test]
        public void S02_TV_G01__MagicBoltExpiresOnTick43()
        {
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            var pr = _k.W.Projectiles[0];
            // 생긴 틱(0)에는 움직이지 않는다(U30).
            _k.Step();
            Assert.AreEqual(ProjectileState.Pending, pr.State);
            Assert.AreEqual(0f, pr.Traveled);
            _k.Step(42); // 틱 1~42
            Assert.IsFalse(pr.Dead);
            Assert.AreEqual(ProjectileState.Flying, pr.State);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.ProjectileExpired));
            _k.Step(); // 틱 43
            Assert.IsTrue(pr.Dead);
            var ev = _k.EventsOf(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(43, ev[0].Tick);
            Assert.AreEqual(0, ev[0].A);
            Assert.GreaterOrEqual(pr.Traveled, 10f - 1e-3f);
            Assert.AreEqual(0, _k.W.Projectiles.Count);
        }

        [Test]
        public void S02_Projectile__MagicBoltStopsAtFirstEnemy()
        {
            var e1 = _k.AddEnemy(4f, 7f);
            var e2 = _k.AddEnemy(5f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            _k.Step(30);
            Assert.AreEqual(10f, Damage(e1), 1e-3f);
            Assert.AreEqual(0f, Damage(e2), 1e-3f);
            var ev = _k.EventsOf(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(2, ev[0].A);
        }

        // TV-G02: 마력탄 + 관통 + 트리거→화염구, 일렬 적 3 → 3체 각 1회, 하위 스코프는 첫 적에서 1회
        [Test]
        public void S02_G02__PierceHitsThreeAndTriggerFiresOnceAtFirstEnemy()
        {
            var e1 = _k.AddEnemy(4f, 7f);
            var e2 = _k.AddEnemy(5f, 7f);
            var e3 = _k.AddEnemy(6f, 7f);
            var s = _k.Compile(B.Mb, B.Pierce, B.Trig, B.Fb, B.None);
            _k.Fire(s);
            var bolt = _k.W.Projectiles[0];

            int spawnedFireballs = 0, boltHits = 0;
            int e1Hits = 0, e2Hits = 0, e3Hits = 0;
            for (int i = 0; i < 20 && !bolt.Dead; i++) _k.Step();
            // 마력탄은 3체를 맞히고(관통 +2) 셋째에서 사라진다.
            Assert.IsTrue(bolt.Dead);
            Assert.AreEqual(0, bolt.PierceLeft);
            Assert.IsTrue(bolt.TriggerFired);
            foreach (var ev in _k.W.Events)
            {
                if (ev.Type == SimEventType.ProjectileSpawned && ev.Id == B.Fb) spawnedFireballs++;
                if (ev.Type == SimEventType.EnemyDamaged && ev.Id == Ids.Element.Arcane)
                {
                    boltHits++;
                    if (ev.EntityId == e1.Id) e1Hits++;
                    if (ev.EntityId == e2.Id) e2Hits++;
                    if (ev.EntityId == e3.Id) e3Hits++;
                    // 크기 없음, 관통 +10% → 11
                    Assert.AreEqual(11, ev.A);
                }
            }
            Assert.AreEqual(3, boltHits);
            Assert.AreEqual(1, e1Hits);
            Assert.AreEqual(1, e2Hits);
            Assert.AreEqual(1, e3Hits);
            Assert.AreEqual(1, spawnedFireballs);
        }

        // TV-G11: 하위 탄은 다음 틱부터 움직이고, 자기를 만든 적과 부딪치지 않는다
        [Test]
        public void S02_G11__ChildSpawnsPendingAtHitPointAndIgnoresItsMaker()
        {
            var e1 = _k.AddEnemy(4f, 7f);
            var s = _k.Compile(B.Mb, B.Trig, B.Mb, B.None, B.None);
            _k.Fire(s);
            var bolt = _k.W.Projectiles[0];
            ProjectileEntity child = null;
            int hitTick = -1;
            for (int i = 0; i < 20 && child == null; i++)
            {
                int tick = _k.W.Tick;
                // Step 안의 Compact가 죽은 부모를 빼므로 먼저 목록을 본다.
                _k.W.Mods.Spells.Stage9_Projectiles(_k.W);
                foreach (var pr in _k.W.Projectiles) if (pr != bolt) child = pr;
                if (child != null) hitTick = tick;
                else _k.W.Tick++;
            }
            Assert.IsNotNull(child);
            Assert.IsTrue(bolt.Dead);
            Assert.AreEqual(ProjectileState.Pending, child.State);
            Assert.AreEqual(hitTick, child.SpawnTick);
            Assert.AreEqual(1, child.ScopeDepth);
            Assert.AreEqual(e1.Id, child.IgnoreEnemyId);
            Assert.AreEqual(bolt.ShotId, child.ShotId);
            // 적중 지점 = 탄 반지름 + 적 반지름만큼 앞
            Assert.AreEqual(4f - (0.12f + 0.3f), child.Pos.x, 1e-3f);
            Assert.AreEqual(7f, child.Pos.y, 1e-3f);
            AssertAngle(0f, Deg(child));
            // 감쇠 0.85
            Assert.AreEqual(0.85f, child.DamageMul, 1e-4f);

            Vector2 at = child.Pos;
            _k.W.Tick++;
            _k.W.Mods.Spells.Stage9_Projectiles(_k.W);
            Assert.AreEqual(ProjectileState.Flying, child.State);
            Assert.Greater(child.Pos.x, at.x);
            // 만든 적 위를 지나가도 맞지 않는다.
            for (int i = 0; i < 10; i++) { _k.W.Tick++; _k.W.Mods.Spells.Stage9_Projectiles(_k.W); }
            Assert.AreEqual(10f, Damage(e1), 1e-3f);
            Assert.IsFalse(child.Dead);
        }

        // TV-G03: 그림자 구체, 일렬 적 5 → 5체 각 1회, 트리거 첫 적에서 1회, 사거리 끝에서 터지지 않고 사라짐
        [Test]
        public void S02_G03__ShadowOrbPassesThroughEveryEnemyOnce()
        {
            var es = new List<EnemyState>();
            for (int i = 0; i < 5; i++) es.Add(_k.AddEnemy(3.5f + i, 7f));
            _k.Fire(_k.Compile(B.Orb, B.Trig, B.Mb, B.None, B.None));
            var orb = _k.W.Projectiles[0];
            _k.Step(90);
            int orbHits = 0, children = 0;
            foreach (var ev in _k.W.Events)
            {
                if (ev.Type == SimEventType.EnemyDamaged && ev.Id == Ids.Element.Dark) { orbHits++; Assert.AreEqual(28, ev.A); }
                if (ev.Type == SimEventType.ProjectileSpawned && ev.B == 1) children++;
            }
            Assert.AreEqual(5, orbHits);
            Assert.AreEqual(5, _k.CountEvents(SimEventType.ProjectilePassed, orb.Id), "적마다 지나감 표시 1번");
            Assert.AreEqual(1, children);
            Assert.IsTrue(orb.Dead);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.Explosion));
            var expired = _k.EventsOf(SimEventType.ProjectileExpired, orb.Id);
            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual(0, expired[0].A);
        }

        // TV-G04: 튕겨서 같은 적 위를 다시 지나도 두 번째 피해 없음. 벽에서(튕김이 없으면) 소멸
        [Test]
        public void S02_G04__ShadowOrbDoesNotHitSameEnemyAfterBounce()
        {
            _k.P.Pos = new Vector2(21f, 7f);
            var e = _k.AddEnemy(22.5f, 7f);
            _k.Fire(_k.Compile(B.Orb, B.Bounce, B.None, B.None, B.None));
            var orb = _k.W.Projectiles[0];
            _k.Step(70);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.ProjectileBounced));
            Assert.AreEqual(28f, Damage(e), 1e-3f);
            Assert.Less(orb.Pos.x, 22f);

            var k2 = new SpellWorld();
            k2.P.Pos = new Vector2(22f, 7f);
            k2.Fire(k2.Compile(B.Orb, B.None, B.None, B.None, B.None));
            var orb2 = k2.W.Projectiles[0];
            k2.Step(30);
            Assert.IsTrue(orb2.Dead);
            var ev = k2.EventsOf(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, ev[0].A);
        }

        // TV-G05: 부메랑, 앞 3칸 적 1 → 같은 적 2회(가는 길 1, 오는 길 1), 트리거 1회
        [Test]
        public void S02_G05__BoomerangHitsSameEnemyTwiceAndTriggersOnce()
        {
            var e = _k.AddEnemy(5f, 7f);
            _k.Fire(_k.Compile(B.Bm, B.Trig, B.Mb, B.None, B.None));
            var bm = _k.W.Projectiles[0];
            bool sawReturning = false;
            for (int i = 0; i < 120 && !bm.Dead; i++)
            {
                _k.Step();
                if (bm.State == ProjectileState.Returning) sawReturning = true;
            }
            Assert.IsTrue(sawReturning);
            Assert.IsTrue(bm.Dead);
            int hits = 0, children = 0;
            foreach (var ev in _k.W.Events)
            {
                if (ev.Type == SimEventType.EnemyDamaged && ev.EntityId == e.Id && ev.A == 15) hits++;
                if (ev.Type == SimEventType.ProjectileSpawned && ev.B == 1) children++;
            }
            Assert.AreEqual(2, hits);
            Assert.AreEqual(1, children);
            var expired = _k.EventsOf(SimEventType.ProjectileExpired, bm.Id);
            Assert.AreEqual(3, expired[0].A);
            // 쏜 사람의 0.5칸 안에서 사라진다.
            Assert.LessOrEqual((bm.Pos - _k.P.Pos).magnitude, 0.5f + 1e-3f);
        }

        // TV-G06: 연쇄 번개, 첫 대상 + 반경 3 안에 적 3 → 같은 틱에 4타 18, 12.6, 8.82, 6.174
        [Test]
        public void S02_G06__ChainLightningFourHitsSameTick()
        {
            var a = _k.AddEnemy(6f, 7f);
            var b = _k.AddEnemy(6f, 5f);
            var c = _k.AddEnemy(6f, 3f);
            var d = _k.AddEnemy(8f, 3f);
            _k.Fire(_k.Compile(B.Cl, B.None, B.None, B.None, B.None));
            _k.Step();
            Assert.AreEqual(18f, Damage(a), 1e-3f);
            Assert.AreEqual(12.6f, Damage(b), 1e-3f);
            Assert.AreEqual(8.82f, Damage(c), 1e-3f);
            Assert.AreEqual(6.174f, Damage(d), 1e-3f);
            var arcs = _k.EventsOf(SimEventType.ChainLightningArc);
            Assert.AreEqual(4, arcs.Count);
            for (int i = 0; i < 4; i++) { Assert.AreEqual(i, arcs[i].A); Assert.AreEqual(0, arcs[i].Tick); }
            Assert.AreEqual(0, _k.W.Projectiles.Count);
        }

        // TV-G07: 첫 대상 주변 3칸 안에 적 없음 → 1타 18, 전이 없음
        [Test]
        public void S02_G07__ChainLightningStopsWithoutNearbyTarget()
        {
            var a = _k.AddEnemy(6f, 7f);
            var far = _k.AddEnemy(6f, 3.9f);
            // 광선 사거리 7 밖의 적은 첫 대상이 못 된다.
            var beyond = _k.AddEnemy(9.5f, 7f, 1000f, 0.3f);
            _k.Fire(_k.Compile(B.Cl, B.None, B.None, B.None, B.None));
            _k.Step();
            Assert.AreEqual(18f, Damage(a), 1e-3f);
            Assert.AreEqual(0f, Damage(far), 1e-3f);
            Assert.AreEqual(0f, Damage(beyond), 1e-3f);
        }

        // TV-G09: 화염구가 A에 적중, 폭발 반경 2 안에 B·C, 2.1칸에 D → A·B·C 각 40, D 없음. 트리거는 폭발 중심에서 1회
        [Test]
        public void S02_G09__FireballExplosionHitsEachEnemyInRadiusOnce()
        {
            var a = _k.AddEnemy(6f, 7f);
            // 폭발 중심 = 적중 지점 = A 중심에서 (탄 반지름 0.18 + 적 반지름 0.3)만큼 앞.
            float cx = 6f - (0.18f + 0.3f);
            // 판정: 적 중심 거리 ≤ 2 + 적 반지름. B·C는 겉면이 2칸 안, D는 겉면이 2.1칸.
            var b = _k.AddEnemy(cx, 7f - (2f + 0.3f - 0.05f));
            var c = _k.AddEnemy(cx, 7f + (2f + 0.3f - 0.05f));
            var d = _k.AddEnemy(cx, 7f + (2.1f + 0.3f));
            _k.Fire(_k.Compile(B.Fb, B.Trig, B.Mb, B.None, B.None));
            _k.Step(40);
            Assert.AreEqual(40f, Damage(a), 1e-3f);
            Assert.AreEqual(40f, Damage(b), 1e-3f);
            Assert.AreEqual(40f, Damage(c), 1e-3f);
            Assert.AreEqual(0f, Damage(d) - ChildDamage(d), 1e-3f);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.EnemyDamaged, a.Id) - ChildHits(a));

            var boom = _k.EventsOf(SimEventType.Explosion);
            Assert.AreEqual(1, boom.Count);
            Assert.AreEqual(2f, boom[0].F, 1e-4f);
            Assert.AreEqual(Ids.Element.Fire, boom[0].Id);
            Assert.AreEqual(cx, boom[0].Pos.x, 1e-3f);

            // 하위 스코프는 폭발 중심에서 1회
            int children = 0;
            foreach (var ev in _k.W.Events)
                if (ev.Type == SimEventType.ProjectileSpawned && ev.B == 1)
                {
                    children++;
                    Assert.AreEqual(cx, ev.Pos.x, 1e-3f);
                    Assert.AreEqual(7f, ev.Pos.y, 1e-3f);
                }
            Assert.AreEqual(1, children);
        }

        float ChildDamage(EnemyState e)
        {
            float sum = 0f;
            foreach (var ev in _k.W.Events)
                if (ev.Type == SimEventType.EnemyDamaged && ev.EntityId == e.Id && ev.Id == Ids.Element.Arcane) sum += ev.A;
            return sum;
        }

        int ChildHits(EnemyState e)
        {
            int n = 0;
            foreach (var ev in _k.W.Events)
                if (ev.Type == SimEventType.EnemyDamaged && ev.EntityId == e.Id && ev.Id == Ids.Element.Arcane) n++;
            return n;
        }

        // TV-G10: 화염구가 빈 벽에 닿음 → 그 자리 폭발, 맞은 적이 없으면 트리거 없음. 사거리 끝에서도 터진다
        [Test]
        public void S02_G10__FireballExplodesAtWallAndRangeEndWithoutTrigger()
        {
            _k.P.Pos = new Vector2(22f, 7f);
            _k.Fire(_k.Compile(B.Fb, B.Trig, B.Mb, B.None, B.None));
            _k.Step(30);
            var boom = _k.EventsOf(SimEventType.Explosion);
            Assert.AreEqual(1, boom.Count);
            Assert.AreEqual(24f, boom[0].Pos.x, 1e-3f);
            Assert.AreEqual(1, _k.EventsOf(SimEventType.ProjectileExpired)[0].A);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.ProjectileSpawned));

            var k2 = new SpellWorld();
            k2.Fire(k2.Compile(B.Fb, B.Trig, B.Mb, B.None, B.None));
            k2.Step(60);
            var boom2 = k2.EventsOf(SimEventType.Explosion);
            Assert.AreEqual(1, boom2.Count);
            Assert.GreaterOrEqual(boom2[0].Pos.x, 2f + 8f - 1e-3f);
            Assert.AreEqual(0, k2.EventsOf(SimEventType.ProjectileExpired)[0].A);
            Assert.AreEqual(1, k2.CountEvents(SimEventType.ProjectileSpawned));
        }

        // TV-F11: 불안정 핵 → 폭발 반경 4 (Traits.ExplosionRadiusMul)
        [Test]
        public void S02_F11__ExplosionRadiusUsesTraitsMultiplier()
        {
            _k.Traits.ExplosionMul = 2f;
            var a = _k.AddEnemy(6f, 7f);
            float cx = 6f - (0.18f + 0.3f);
            var far = _k.AddEnemy(cx, 7f + 4f);
            _k.Fire(_k.Compile(B.Fb, B.None, B.None, B.None, B.None));
            _k.Step(40);
            Assert.AreEqual(4f, _k.EventsOf(SimEventType.Explosion)[0].F, 1e-4f);
            Assert.AreEqual(40f, Damage(far), 1e-3f);
        }

        // TV-G12: 벽 튕김 1개, 수직 입사 → 방향 반전, 남은 튕김 1, 이동 거리 누적
        [Test]
        public void S02_G12__WallBounceReflectsAndKeepsTravelling()
        {
            _k.P.Pos = new Vector2(22f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.Bounce, B.None, B.None, B.None));
            var pr = _k.W.Projectiles[0];
            Assert.AreEqual(2, pr.BouncesLeft);
            int bounceTick = -1;
            for (int i = 0; i < 20 && bounceTick < 0; i++)
            {
                _k.Step();
                if (_k.CountEvents(SimEventType.ProjectileBounced) > 0) bounceTick = _k.W.Tick - 1;
            }
            Assert.GreaterOrEqual(bounceTick, 0);
            Assert.AreEqual(1, pr.BouncesLeft);
            AssertAngle(180f, Deg(pr));
            Assert.IsFalse(pr.Dead);
            // 이동 거리 = 틱 수 × 14/60 (튕겨도 이어서 센다)
            Assert.AreEqual(bounceTick * 14f / 60f, pr.Traveled, 1e-3f);
            Assert.AreEqual(24f - (pr.Traveled - 2f), pr.Pos.x, 1e-3f);
            Assert.AreEqual(24f, _k.EventsOf(SimEventType.ProjectileBounced)[0].Pos.x, 1e-3f);
            // 사거리 10에서 끝난다(벽에서 초기화되지 않는다).
            _k.Step(43 - _k.W.Tick);
            Assert.IsFalse(pr.Dead);
            _k.Step();
            Assert.IsTrue(pr.Dead);
        }

        [Test]
        public void S02_Projectile__NoBounceDiesAtWall()
        {
            _k.P.Pos = new Vector2(22f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            _k.Step(15);
            var ev = _k.EventsOf(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(1, ev[0].A);
            Assert.AreEqual(24f, ev[0].Pos.x, 1e-3f);
        }

        // TV-G15: 땅속 적 위를 탄이 지남 → 피해·효과 없음, 탄 소모 없음
        [Test]
        public void S02_G15__BurrowedEnemyIsPassedWithoutConsumingShot()
        {
            var burrowed = _k.AddEnemy(4f, 7f);
            burrowed.Burrowed = true;
            var behind = _k.AddEnemy(6f, 7f);
            _k.Fire(_k.Compile(B.Ice, B.None, B.None, B.None, B.None));
            _k.Step(30);
            Assert.AreEqual(0f, Damage(burrowed), 1e-3f);
            Assert.AreEqual(14f, Damage(behind), 1e-3f);
            Assert.AreEqual(1, _k.Enemies.Slows.Count);
            Assert.AreEqual(behind.Id, _k.Enemies.Slows[0].EnemyId);
        }

        // 03 요약서 7.2절: 크기 2배는 반지름 ×2, 겹치면 곱, 최대 ×4
        [Test]
        public void S02_U8__SizeDoublesRadiusUpToFour()
        {
            _k.Fire(_k.Compile(B.Mb, B.Size, B.None, B.None, B.None));
            Assert.AreEqual(0.24f, _k.W.Projectiles[0].Radius, 1e-5f);
            _k.Fire(_k.Compile(B.Mb, B.Size, B.Size, B.Size, B.None));
            Assert.AreEqual(0.48f, _k.W.Projectiles[1].Radius, 1e-5f);
        }

        // 03 요약서 7.2절: 유도 = 시야 앞 ±90도, 6칸 안 가장 가까운 적으로 틱당 최대 3도
        [Test]
        public void S02_U9__HomingTurnsThreeDegreesPerTickTowardNearestInCone()
        {
            var behind = _k.AddEnemy(1f, 7f);       // 뒤쪽(원뿔 밖)
            var target = _k.AddEnemy(5f, 4f);       // 앞쪽 위(화면 기준 반시계 쪽)
            _k.Fire(_k.Compile(B.Mb, B.Homing, B.None, B.None, B.None));
            var pr = _k.W.Projectiles[0];
            _k.Step();
            AssertAngle(0f, Deg(pr));
            _k.Step();
            Assert.AreEqual(target.Id, pr.HomingTargetId);
            AssertAngle(3f, Deg(pr));
            _k.Step();
            AssertAngle(6f, Deg(pr));
            Assert.AreEqual(0f, Damage(behind), 1e-3f);
        }

        [Test]
        public void S02_Projectile__OutsideCombatShotsPassThroughEnemies()
        {
            _k.W.Run.Phase = RunPhase.BossIntro;
            var e = _k.AddEnemy(4f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            _k.Step(20);
            Assert.AreEqual(0f, Damage(e), 1e-3f);
            Assert.AreEqual(1, _k.LiveProjectiles());
        }

        [Test]
        public void S02_ClearProjectiles__RemovesOwnersShots()
        {
            _k.Fire(_k.Compile(B.Mb, B.S3, B.None, B.None, B.None));
            _k.Sys.ClearProjectiles(_k.W, 1);
            Assert.AreEqual(3, _k.LiveProjectiles());
            _k.Sys.ClearProjectiles(_k.W, -1);
            Assert.AreEqual(0, _k.LiveProjectiles());
            _k.Step();
            Assert.AreEqual(0, _k.W.Projectiles.Count);
        }

        [Test]
        public void S02_G16__SameSeedSameResult()
        {
            string Run()
            {
                var k = new SpellWorld();
                for (int i = 0; i < 6; i++) k.AddEnemy(4f + i * 1.5f, 6f + (i % 3), 60f);
                var s = k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.Frost);
                var sb = new System.Text.StringBuilder();
                for (int t = 0; t < 120; t++)
                {
                    if (t % 9 == 0) k.Fire(s);
                    k.Step();
                }
                foreach (var e in k.W.Enemies) sb.Append(e.Id).Append(':').Append(e.Hp.ToString("R")).Append(';');
                sb.Append(k.W.Projectiles.Count).Append('/').Append(k.W.Events.Count);
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        // ------------------------------------------------------------ 보스 마디 (05 요약서 9.6절)

        EnemyState SerpentBody(float segmentRadius)
        {
            // 몸통(머리)은 탄 길에서 멀리, 마디 하나가 탄 길(y = 7) 위에 있다.
            var e = _k.AddEnemy(12f, 2f, 1000f, 0.6f, Ids.Boss.NightSerpent);
            e.IsBoss = true;
            var boss = new BossState { EnemyId = e.Id, SegmentRadius = segmentRadius, State = BossFightState.Waiting };
            boss.Segments.Add(new Vector2(8f, 7f));
            _k.W.Boss = boss;
            return e;
        }

        [Test]
        public void S05_9_6__PlayerShotHitsBossSegment()
        {
            var e = SerpentBody(0.5f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None), 0f);
            _k.Step(60);
            Assert.Greater(Damage(e), 0f, "마디에 맞으면 보스 몸통이 피해를 받는다");
            Assert.AreEqual(1, _k.Enemies.HitPositions.Count);
            // 맞은 자리 = 마디 겉면(부위 판정용).
            Assert.AreEqual(0.5f, (_k.Enemies.HitPositions[0] - new Vector2(8f, 7f)).magnitude, 1e-3f);
        }

        [Test]
        public void S05_9_6__NoSegmentRadiusMeansNoSegmentHit()
        {
            var e = SerpentBody(0f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None), 0f);
            _k.Step(60);
            Assert.AreEqual(0f, Damage(e));
        }
    }
}
