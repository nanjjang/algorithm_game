using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Traits;

namespace Wizard.Solo.Tests.Traits
{
    /// <summary>변이 6종 중 Traits가 맡는 것(02 요약서 9.1절, 03 요약서 11절, 07 요약서 4.7절).</summary>
    public class TraitMutationTests
    {
        const int UnstableChancePercent = 5;      // 02 요약서 9.1절: 발사 1회당 5%
        const int WarnTicks = 18;                 // 0.3초
        const int FireballInterval = 36;          // 화염구 0.6초

        static HitInfo Kill(EnemyState e) => new HitInfo { AttackerSlot = 0, EnemyId = e.Id, Killed = true, Amount = 10f };

        // ------------------------------------------------------------ 뒤틀린 총열

        // 02 요약서 9.1절: 발사마다 [−25°, +25°] 정수도 균등. 호출 1번에 난수 1번.
        [Test]
        public void S04_11__TwistedBarrelSpreadIsIntegerWithin25()
        {
            const ulong seed = 11;
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, seed, Ids.Mutation.TwistedBarrel);
            var reference = Pcg32.Derive(seed, RngStream.Combat);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 2000; i++)
            {
                float deg = rig.Traits.RollSpreadDegrees(rig.W, rig.P);
                Assert.AreEqual(reference.Range(-25, 26), deg, 0f, "roll " + i);
                Assert.AreEqual(UnityEngine.Mathf.Round(deg), deg, 0f);
                Assert.GreaterOrEqual(deg, -25f);
                Assert.LessOrEqual(deg, 25f);
                if (deg == -25f) sawMin = true;
                if (deg == 25f) sawMax = true;
            }
            Assert.IsTrue(sawMin && sawMax, "두 끝 값이 모두 나온다");
        }

        [Test]
        public void S04_11__NoSpreadAndNoRngWithoutTwistedBarrel()
        {
            const ulong seed = 12;
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, seed);
            for (int i = 0; i < 10; i++) Assert.AreEqual(0f, rig.Traits.RollSpreadDegrees(rig.W, rig.P));
            Assert.AreEqual(Pcg32.Derive(seed, RngStream.Combat).NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        // ------------------------------------------------------------ 불안정 핵

        // 02 요약서 9.1절: 폭발 반경 +100%(배율 2). 없으면 1.
        [Test]
        public void S04_11__UnstableCoreExplosionRadiusMul()
        {
            var with = new TraitRig("char.1", 0, Ids.Block.Fireball, 1, Ids.Mutation.UnstableCore);
            Assert.AreEqual(2f, with.Traits.ExplosionRadiusMul(with.P), 1e-6f);
            var without = new TraitRig("char.1", 0, Ids.Block.Fireball, 1);
            Assert.AreEqual(1f, without.Traits.ExplosionRadiusMul(without.P), 1e-6f);
        }

        // 계약: 같은 씨앗에서 발사 n번에 난수 소비 n번. RollUnstableCore는 여러 번 불러도 값이 안 변하고 난수를 쓰지 않는다.
        // (판의 첫 발사분은 RecomputeDerived에서, 그다음부터는 OnPlayerFired에서 다음 발사분을 굴린다.)
        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(987654321UL)]
        public void Contract__UnstableCoreConsumesOneRollPerShot(ulong seed)
        {
            var rig = new TraitRig("char.1", 0, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
            var reference = Pcg32.Derive(seed, RngStream.Combat);
            bool expected = reference.RollPercent(UnstableChancePercent);        // 첫 발사분(RecomputeDerived)
            const int shots = 300;
            for (int n = 0; n < shots; n++)
            {
                rig.W.Tick = n * FireballInterval;
                for (int k = 0; k < 3; k++)
                    Assert.AreEqual(expected, rig.Traits.RollUnstableCore(rig.W, rig.P), "shot " + n + " call " + k);
                rig.Traits.OnPlayerFired(rig.W, rig.P, n + 1, FireballInterval);
                expected = reference.RollPercent(UnstableChancePercent);         // 다음 발사분
            }
            // 발사 300번 + 첫 발사분 1번 = 301번 굴림. 그다음 수가 기준 난수기와 같아야 한다.
            Assert.AreEqual(reference.NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        // 유리 지팡이 임시 하락 뒤의 RecomputeDerived는 굴려 둔 발사분을 다시 굴리지 않는다.
        [Test]
        public void Contract__RecomputeDoesNotRerollPendingShot()
        {
            const ulong seed = 5;
            var rig = new TraitRig("char.1", 10, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
            rig.Traits.RecomputeDerived(rig.W, rig.P);
            rig.Traits.RecomputeDerived(rig.W, rig.P);
            var reference = Pcg32.Derive(seed, RngStream.Combat);
            reference.RollPercent(UnstableChancePercent);
            Assert.AreEqual(reference.NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        // 02 요약서 9.1절: 코어가 폭발 블록일 때만. 코어가 마력탄이면 자폭이 없고 난수도 쓰지 않는다.
        [Test]
        public void S04_11__UnstableCoreNeedsExplosiveCore()
        {
            const ulong seed = 3;
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, seed, Ids.Mutation.UnstableCore);
            for (int n = 0; n < 200; n++)
            {
                rig.W.Tick = n * 9;
                Assert.IsFalse(rig.Traits.RollUnstableCore(rig.W, rig.P));
                rig.Traits.OnPlayerFired(rig.W, rig.P, n + 1, 9);
                rig.W.Step();
            }
            Assert.AreEqual(0, rig.CountEvents(SimEventType.UnstableCoreWarn));
            Assert.AreEqual(Pcg32.Derive(seed, RngStream.Combat).NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        [Test]
        public void S04_11__NoUnstableCoreWithoutMutation()
        {
            const ulong seed = 4;
            var rig = new TraitRig("char.1", 0, Ids.Block.Fireball, seed);
            for (int n = 0; n < 200; n++)
            {
                Assert.IsFalse(rig.Traits.RollUnstableCore(rig.W, rig.P));
                rig.Traits.OnPlayerFired(rig.W, rig.P, n + 1, FireballInterval);
            }
            Assert.AreEqual(Pcg32.Derive(seed, RngStream.Combat).NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        // 02 요약서 9.1절: 자폭 0.3초 전 예고 UnstableCoreWarn. 화염구(간격 36틱 > 18틱)를 이어 쏘면 예고는 자폭 발사의 정확히 18틱 전
        // 틱에 나고, 자폭이 아닌 발사 앞에는 예고가 없다. 틱 순서는 실제와 같다: 6단계(발사) → 11단계(Stage11_TickEnd).
        [Test]
        public void S04_11__UnstableCoreWarnPrecedesBlowupBy18Ticks()
        {
            int blowups = 0;
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var rig = new TraitRig("char.1", 0, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
                int scannedEvents = 0;
                for (int n = 0; n < 400; n++)
                {
                    int t = rig.W.Tick;
                    Assert.AreEqual(n * FireballInterval, t);
                    // 지난 발사 뒤로 난 예고(이번 발사 전까지)
                    var warnTicks = new List<int>();
                    for (; scannedEvents < rig.W.Events.Count; scannedEvents++)
                        if (rig.W.Events[scannedEvents].Type == SimEventType.UnstableCoreWarn)
                        {
                            Assert.AreEqual(0, rig.W.Events[scannedEvents].Slot);
                            warnTicks.Add(rig.W.Events[scannedEvents].Tick);
                        }

                    bool explodes = rig.Traits.RollUnstableCore(rig.W, rig.P);
                    if (explodes)
                    {
                        blowups++;
                        Assert.IsNotEmpty(warnTicks, "seed " + seed + " shot " + n + ": 예고가 자폭보다 먼저 난다");
                        if (n == 0) Assert.AreEqual(0, warnTicks[0], "판의 첫 발사분은 굴린 그 자리에서 예고");
                        else Assert.AreEqual(t - WarnTicks, warnTicks[0], "seed " + seed + " shot " + n);
                    }
                    else Assert.IsEmpty(warnTicks, "seed " + seed + " shot " + n + ": 자폭이 아니면 예고 없음");

                    rig.Traits.OnPlayerFired(rig.W, rig.P, n + 1, FireballInterval);
                    for (int k = 0; k < FireballInterval; k++) rig.W.Step();
                }
            }
            Assert.Greater(blowups, 0, "발사 2,000번 안에 자폭이 한 번은 나와야 이 테스트가 뜻이 있다");
        }

        // 발사 간격이 예고 시간(18틱) 이하면 예고는 직전 발사 때(그 틱에) 낸다.
        [Test]
        public void S04_11__UnstableCoreWarnAtPreviousShotWhenIntervalIsShort()
        {
            const int interval = 9;
            int blowups = 0;
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var rig = new TraitRig("char.1", 0, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
                int scannedEvents = 0;
                for (int n = 0; n < 400; n++)
                {
                    int t = rig.W.Tick;
                    var warnTicks = new List<int>();
                    for (; scannedEvents < rig.W.Events.Count; scannedEvents++)
                        if (rig.W.Events[scannedEvents].Type == SimEventType.UnstableCoreWarn)
                            warnTicks.Add(rig.W.Events[scannedEvents].Tick);

                    bool explodes = rig.Traits.RollUnstableCore(rig.W, rig.P);
                    if (explodes)
                    {
                        blowups++;
                        Assert.IsNotEmpty(warnTicks);
                        Assert.AreEqual(n == 0 ? 0 : t - interval, warnTicks[0], "seed " + seed + " shot " + n);
                    }
                    else Assert.IsEmpty(warnTicks);

                    rig.Traits.OnPlayerFired(rig.W, rig.P, n + 1, interval);
                    for (int k = 0; k < interval; k++) rig.W.Step();
                }
            }
            Assert.Greater(blowups, 0);
        }

        // 발사를 멈췄다 다시 쏴도 자폭 앞 0.3초 안에 예고가 하나는 있다(0.3초마다 다시 낸다). 공격할 수 없는 동안(야영)에는 내지 않는다.
        [Test]
        public void S04_11__UnstableCoreWarnRepeatsWhileBlowupIsPending()
        {
            TraitRig rig = null;
            int fireTick = 0;
            for (ulong seed = 1; seed <= 400 && rig == null; seed++)
            {
                var candidate = new TraitRig("char.1", 0, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
                for (int n = 0; n < 50; n++)
                {
                    // 굴려 둔 결과를 쓰지 않고 들여다본다(RollUnstableCore는 실제 발사 때만 부른다).
                    var scratch = (TraitScratch)candidate.P.TraitScratch;
                    if (scratch.HasUnstableRoll && scratch.UnstableExplodes) { rig = candidate; fireTick = candidate.W.Tick; break; }
                    Assert.IsFalse(candidate.Traits.RollUnstableCore(candidate.W, candidate.P));
                    candidate.Traits.OnPlayerFired(candidate.W, candidate.P, n + 1, FireballInterval);
                    for (int k = 0; k < FireballInterval; k++) candidate.W.Step();
                }
            }
            Assert.IsNotNull(rig, "자폭 발사분이 나오는 씨앗을 찾지 못했다");

            // 자폭으로 정해진 발사를 쏘지 않고 200틱을 보낸다.
            int before = rig.CountEvents(SimEventType.UnstableCoreWarn);
            for (int k = 0; k < 200; k++) rig.W.Step();
            int during = rig.CountEvents(SimEventType.UnstableCoreWarn) - before;
            Assert.GreaterOrEqual(during, 200 / WarnTicks - 1);
            int lastWarnTick = -1;
            for (int i = 0; i < rig.W.Events.Count; i++)
                if (rig.W.Events[i].Type == SimEventType.UnstableCoreWarn) lastWarnTick = rig.W.Events[i].Tick;
            Assert.LessOrEqual(rig.W.Tick - lastWarnTick, WarnTicks);
            Assert.Greater(rig.W.Tick, fireTick);

            // 야영에서는 내지 않는다.
            rig.W.Run.Phase = RunPhase.Camp;
            int atCamp = rig.CountEvents(SimEventType.UnstableCoreWarn);
            for (int k = 0; k < 100; k++) rig.W.Step();
            Assert.AreEqual(atCamp, rig.CountEvents(SimEventType.UnstableCoreWarn));

            // 굴려 둔 결과는 그대로 남아 있다.
            rig.W.Run.Phase = RunPhase.WaveCombat;
            Assert.IsTrue(rig.Traits.RollUnstableCore(rig.W, rig.P));
        }

        // 계약 밖 방어: RollUnstableCore가 돌려준 결과는 그 틱에만 같다. OnPlayerFired 없이 틱이 넘어가면(발사가 끝내 안 나감)
        // 그 결과를 다시 쓰지 않고 다음 물음에서 새로 굴린다 — 자폭 결과가 남아 발사마다 터지는 일을 막는다.
        [Test]
        public void Contract__UnstableCoreConsumedRollIsNotReusedOnALaterTick()
        {
            const ulong seed = 9;
            var rig = new TraitRig("char.1", 0, Ids.Block.Fireball, seed, Ids.Mutation.UnstableCore);
            var reference = Pcg32.Derive(seed, RngStream.Combat);
            bool first = reference.RollPercent(UnstableChancePercent);
            Assert.AreEqual(first, rig.Traits.RollUnstableCore(rig.W, rig.P));
            Assert.AreEqual(first, rig.Traits.RollUnstableCore(rig.W, rig.P));
            rig.W.Tick = 40;
            bool second = reference.RollPercent(UnstableChancePercent);
            Assert.AreEqual(second, rig.Traits.RollUnstableCore(rig.W, rig.P));
            Assert.AreEqual(second, rig.Traits.RollUnstableCore(rig.W, rig.P));
            Assert.AreEqual(reference.NextU32(), rig.W.Rng(RngStream.Combat).NextU32());
        }

        // ------------------------------------------------------------ 탐식

        // 02 요약서 9.1절: 처치당 열 −4(−480 HU), 최근 60틱 합 ≤ 20(2,400 HU).
        [Test]
        public void S01_7_6__GluttonyPerKillAndPerSecondCap()
        {
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, 1, Ids.Mutation.Gluttony);
            rig.P.HeatHu = 9000;
            rig.W.Tick = 100;
            for (int i = 0; i < 5; i++)
            {
                var e = rig.AddEnemy(13f, 7f);
                rig.Traits.OnEnemyKilled(rig.W, e, Kill(e));
            }
            Assert.AreEqual(5, rig.Player.Reduced.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual(480, rig.Player.Reduced[i]);
            Assert.AreEqual(9000 - 2400, rig.P.HeatHu);

            // 여섯째: 같은 1초 안이라 더 깎지 않는다.
            var sixth = rig.AddEnemy(13f, 7f);
            rig.Traits.OnEnemyKilled(rig.W, sixth, Kill(sixth));
            Assert.AreEqual(5, rig.Player.Reduced.Count);

            // 59틱 뒤에도 창 안이다.
            rig.W.Tick = 159;
            rig.Traits.Stage11_TickEnd(rig.W);
            rig.Traits.OnEnemyKilled(rig.W, sixth, Kill(sixth));
            Assert.AreEqual(5, rig.Player.Reduced.Count);

            // 60틱 뒤에는 창이 비어 다시 깎는다.
            rig.W.Tick = 160;
            rig.Traits.Stage11_TickEnd(rig.W);
            rig.Traits.OnEnemyKilled(rig.W, sixth, Kill(sixth));
            Assert.AreEqual(6, rig.Player.Reduced.Count);
            Assert.AreEqual(480, rig.Player.Reduced[5]);
            Assert.AreEqual(9000 - 2880, rig.P.HeatHu);
        }

        // 창은 미끄러진다: 10틱마다 한 번씩 처치하면 어느 60틱 구간에서도 합이 2,400 HU를 넘지 않는다.
        [Test]
        public void S01_7_6__GluttonySlidingWindowNeverExceedsCap()
        {
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, 1, Ids.Mutation.Gluttony);
            rig.P.HeatHu = 1000000;
            var perTick = new int[600];
            for (int t = 0; t < 600; t++)
            {
                rig.W.Tick = t;
                int before = rig.Player.Reduced.Count;
                for (int k = 0; k < 2; k++)
                {
                    var e = rig.AddEnemy(13f, 7f);
                    rig.Traits.OnEnemyKilled(rig.W, e, Kill(e));
                }
                for (int i = before; i < rig.Player.Reduced.Count; i++) perTick[t] += rig.Player.Reduced[i];
                rig.Traits.Stage11_TickEnd(rig.W);
            }
            int total = 0;
            for (int t = 0; t < 600; t++)
            {
                total += perTick[t];
                int window = 0;
                for (int k = t; k > t - 60 && k >= 0; k--) window += perTick[k];
                Assert.LessOrEqual(window, 2400, "tick " + t);
            }
            Assert.AreEqual(10 * 2400, total, "10초 동안 초당 상한을 꽉 채운다");
        }

        // 잠금 중 처치는 깎지 않는다. 변이가 없으면 아무 일도 없다.
        [Test]
        public void S01_7_6__GluttonyNotWhileLockedOrWithoutMutation()
        {
            var rig = new TraitRig("char.1", 0, Ids.Block.MagicBolt, 1, Ids.Mutation.Gluttony);
            rig.P.HeatHu = 12480;
            rig.P.Locked = true;
            var e = rig.AddEnemy(13f, 7f);
            rig.Traits.OnEnemyKilled(rig.W, e, Kill(e));
            Assert.AreEqual(0, rig.Player.Reduced.Count);
            Assert.AreEqual(12480, rig.P.HeatHu);

            // 잠금 중 처치는 창에도 들지 않는다: 풀린 뒤 같은 틱에 다섯 번을 다 깎을 수 있다.
            rig.P.Locked = false;
            for (int i = 0; i < 5; i++) rig.Traits.OnEnemyKilled(rig.W, e, Kill(e));
            Assert.AreEqual(5, rig.Player.Reduced.Count);

            var plain = new TraitRig("char.1");
            plain.P.HeatHu = 6000;
            var e2 = plain.AddEnemy(13f, 7f);
            plain.Traits.OnEnemyKilled(plain.W, e2, Kill(e2));
            Assert.AreEqual(0, plain.Player.Reduced.Count);
            Assert.AreEqual(6000, plain.P.HeatHu);
        }

        // ------------------------------------------------------------ 유리 지팡이

        // 02 요약서 9.1절: 피격 시 판 안 임시 강화 −1, 웨이브당 1회. 07 요약서 4.10절: +10 → 1.15^9.
        [Test]
        public void S04_TV_108__GlassWandDropsOncePerWave()
        {
            var rig = new TraitRig("char.1", 10, Ids.Block.MagicBolt, 1, Ids.Mutation.GlassWand);
            Assert.AreEqual(4.045558f, rig.P.Derived.EnhanceMul, 1e-5f);

            rig.Traits.OnPlayerHit(rig.W, rig.P);
            Assert.AreEqual(1, rig.P.Wand.TempStageDrop);
            Assert.AreEqual(9, rig.P.Wand.EffectiveStage);
            Assert.AreEqual(10, rig.P.Wand.Stage, "지팡이 개체의 단계는 그대로");
            Assert.IsTrue(rig.P.Wand.GlassDroppedThisWave);
            Assert.AreEqual(3.517876f, rig.P.Derived.EnhanceMul, 1e-5f);
            Assert.AreEqual(1, rig.CountEvents(SimEventType.WandTempStageDropped));
            var ev = rig.W.Events[rig.W.Events.Count - 1];
            Assert.AreEqual(SimEventType.WandTempStageDropped, ev.Type);
            Assert.AreEqual(9, ev.A);
            Assert.AreEqual(0, ev.Slot);

            // 같은 웨이브의 두 번째 피격: 그대로.
            rig.Traits.OnPlayerHit(rig.W, rig.P);
            Assert.AreEqual(1, rig.P.Wand.TempStageDrop);
            Assert.AreEqual(1, rig.CountEvents(SimEventType.WandTempStageDropped));

            // 다음 웨이브: 다시 한 번.
            rig.Traits.OnWaveStart(rig.W);
            Assert.IsFalse(rig.P.Wand.GlassDroppedThisWave);
            rig.Traits.OnPlayerHit(rig.W, rig.P);
            Assert.AreEqual(2, rig.P.Wand.TempStageDrop);
            Assert.AreEqual(3.059023f, rig.P.Derived.EnhanceMul, 1e-5f);
            Assert.AreEqual(1, rig.P.Derived.CritBonus, "임계 배율 +1은 그대로");
        }

        // +3에서는 안 내려간다. +4는 +3까지만.
        [Test]
        public void S04_11__GlassWandFloorIsStage3()
        {
            var at3 = new TraitRig("char.1", 3, Ids.Block.MagicBolt, 1, Ids.Mutation.GlassWand);
            at3.Traits.OnPlayerHit(at3.W, at3.P);
            Assert.AreEqual(0, at3.P.Wand.TempStageDrop);
            Assert.AreEqual(0, at3.CountEvents(SimEventType.WandTempStageDropped));
            Assert.AreEqual(1.520875f, at3.P.Derived.EnhanceMul, 1e-5f);

            var at0 = new TraitRig("char.1", 0, Ids.Block.MagicBolt, 1, Ids.Mutation.GlassWand);
            at0.Traits.OnPlayerHit(at0.W, at0.P);
            Assert.AreEqual(0, at0.P.Wand.TempStageDrop);

            var at4 = new TraitRig("char.1", 4, Ids.Block.MagicBolt, 1, Ids.Mutation.GlassWand);
            at4.Traits.OnPlayerHit(at4.W, at4.P);
            Assert.AreEqual(3, at4.P.Wand.EffectiveStage);
            for (int wave = 0; wave < 5; wave++)
            {
                at4.Traits.OnWaveStart(at4.W);
                at4.Traits.OnPlayerHit(at4.W, at4.P);
            }
            Assert.AreEqual(3, at4.P.Wand.EffectiveStage);
            Assert.AreEqual(1, at4.CountEvents(SimEventType.WandTempStageDropped));
        }

        [Test]
        public void S04_11__NoStageDropWithoutGlassWand()
        {
            var rig = new TraitRig("char.1", 10);
            rig.Traits.OnPlayerHit(rig.W, rig.P);
            Assert.AreEqual(0, rig.P.Wand.TempStageDrop);
            Assert.AreEqual(0, rig.CountEvents(SimEventType.WandTempStageDropped));
        }
    }
}
