using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Player;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>과열: J 발사, 임계, 잠금, 냉각, 열 링, 불가마 흡수, 불안정 핵, 연습 옵션. 02 요약서 6절.</summary>
    public class PlayerHeatTests
    {
        const int Hu = 120;   // 열 1 = 120 HU

        /// <summary>실행 기록에서 피해 합(마력탄 10, 임계 ×2).</summary>
        static int Damage(Kit k, int fromShot, int count)
        {
            int sum = 0;
            for (int i = fromShot; i < fromShot + count; i++) sum += Kit.BoltDamage * (k.Spells.Shots[i].IsCrit ? 2 : 1);
            return sum;
        }

        static int Crits(Kit k, int count)
        {
            int n = 0;
            for (int i = 0; i < count; i++) if (k.Spells.Shots[i].IsCrit) n++;
            return n;
        }

        // T-H01: 발사 틱 0, 9, …, 225(26발). k번째 발사 뒤 열 4k. 1~21발 일반, 22발(88)~26발 임계. 26발째(104) 잠금, UnlockTick 414.
        [Test]
        public void S01_T_H01__HoldFireLocksAtShot26()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Fire);
            for (int tick = 0; tick <= 225; tick++)
            {
                k.Step();
                int shots = k.Spells.Shots.Count;
                Assert.AreEqual(tick / 9 + 1, shots, "tick " + tick);
                if (tick % 9 == 0 && shots < 26) Assert.AreEqual(4 * Hu * shots, k.P.HeatHu);
            }
            Assert.AreEqual(26, k.Spells.Shots.Count);
            for (int i = 0; i < 26; i++)
            {
                Assert.AreEqual(i * 9, k.Spells.ShotTicks[i]);
                Assert.AreEqual(i + 1 >= 22, k.Spells.Shots[i].IsCrit, "shot " + (i + 1));
            }
            Assert.AreEqual(104 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.P.Locked);
            Assert.AreEqual(414, k.P.UnlockTick);
            Assert.AreEqual(225, k.P.LockStartTick);
            Assert.AreEqual(1, k.Count(SimEventType.HeatLockEntered));
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatLockEntered, out e));
            Assert.AreEqual(9 + 180, e.A);
            CollectionAssert.AreEqual(new[] { false }, k.Traits.LockEntered);
            Assert.AreEqual(HeatRingState.Locked, k.Sys.RingState(k.W, k.P));
        }

        // T-H02: 틱 414 잠금 풀림, 열 0, 같은 틱에 다음 발사(열 4). 주기 414틱 = 6.9초, 피해 310 → 44.93 DPS.
        [Test]
        public void S01_T_H02__UnlockAtTick414AndFireSameTick()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Fire);
            k.StepTo(414);
            Assert.IsTrue(k.P.Locked);
            Assert.AreEqual(26, k.Spells.Shots.Count);
            Assert.AreEqual(104 * Hu, k.P.HeatHu, "잠금 중에는 식지 않는다(T-H14)");
            k.Step();
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(27, k.Spells.Shots.Count);
            Assert.AreEqual(414, k.Spells.ShotTicks[26]);
            Assert.AreEqual(4 * Hu, k.P.HeatHu);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatUnlocked, out e));
            Assert.AreEqual((int)HeatUnlockReason.Timeout, e.A);
            Assert.AreEqual(310, Damage(k, 0, 26));
            Assert.AreEqual(44.93, 310 / (414 / 60.0), 0.005);
        }

        // T-H05~08: 꾹 누르기 1주기 — 임계 수, 피해, 주기, DPS.
        [TestCase("char.1", 7, 330, 414, 47.83)]
        [TestCase("char.2", 5, 310, 384, 48.44)]
        [TestCase("char.3", 5, 310, 444, 41.89)]
        [TestCase("char.4", 4, 300, 414, 43.48)]
        public void S01_T_H05_H08__HoldFireCyclePerCharacter(string charId, int crits, int damage, int period, double dps)
        {
            var k = Kit.Create(charId);
            k.Press(LogicalKey.Fire);
            k.StepTo(period + 1);
            // 27번째 발사가 정확히 주기 틱에 나간다.
            Assert.AreEqual(27, k.Spells.Shots.Count);
            Assert.AreEqual(period, k.Spells.ShotTicks[26]);
            Assert.AreEqual(crits, Crits(k, 26));
            Assert.AreEqual(damage, Damage(k, 0, 26));
            Assert.AreEqual(dps, damage / (period / 60.0), 0.005);
        }

        // T-H05: 달구는 20발째(80)부터 임계. T-H08: 성에는 23발째(92)부터.
        [TestCase("char.1", 20)]
        [TestCase("char.4", 23)]
        [TestCase(null, 22)]
        public void S01_T_H05_H08__FirstCritShot(string charId, int firstCrit)
        {
            var k = Kit.Create(charId);
            k.Press(LogicalKey.Fire);
            k.StepTo(226);
            for (int i = 0; i < 26; i++) Assert.AreEqual(i + 1 >= firstCrit, k.Spells.Shots[i].IsCrit, "shot " + (i + 1));
        }

        // T-H03: 열 96 + 마력탄 → 100, 임계, 잠금 아님.
        [Test]
        public void S01_T_H03__ExactlyHundredIsNotLock()
        {
            var k = Kit.Create();
            k.P.HeatHu = 96 * Hu;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(100 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.Spells.Shots[0].IsCrit);
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(HeatRingState.Brink, k.Sys.RingState(k.W, k.P));
        }

        // T-H04: 열 97 + 마력탄 → 101, 임계 + 잠금.
        [Test]
        public void S01_T_H04__OverHundredLocksAndShotIsCrit()
        {
            var k = Kit.Create();
            k.P.HeatHu = 97 * Hu;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(101 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.Spells.Shots[0].IsCrit);
            Assert.IsTrue(k.P.Locked);
            Assert.AreEqual(0 + 9 + 180, k.P.UnlockTick);
        }

        // T-H09: 열 84, 틱 0·9·18·27 발사 후 뗌 → 88·92·96·100 모두 임계, 잠금 없음.
        // 틱 48의 끝부터 냉각(27 + 9 + 12). 틱 69의 끝 83.5(10,020 HU). 틱 70에 다시 쏘면 87.5로 임계.
        [Test]
        public void S01_T_H09__CritZoneThenCoolFromTick48()
        {
            var k = Kit.Create();
            k.P.HeatHu = 84 * Hu;
            k.Press(LogicalKey.Fire);
            k.StepTo(28);
            k.Release(LogicalKey.Fire);
            Assert.AreEqual(4, k.Spells.Shots.Count);
            for (int i = 0; i < 4; i++) Assert.IsTrue(k.Spells.Shots[i].IsCrit);
            Assert.AreEqual(100 * Hu, k.P.HeatHu);
            Assert.IsFalse(k.P.Locked);
            k.StepTo(48);
            Assert.AreEqual(100 * Hu, k.P.HeatHu, "틱 47의 끝까지는 식지 않는다");
            k.Step();
            Assert.AreEqual(100 * Hu - 90, k.P.HeatHu, "틱 48의 끝에 처음 식는다");
            k.StepTo(70);
            Assert.AreEqual(10020, k.P.HeatHu);
            Assert.AreEqual(4, k.Spells.Shots.Count);
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(5, k.Spells.Shots.Count);
            Assert.AreEqual(10500, k.P.HeatHu);   // 87.5
            Assert.IsTrue(k.Spells.Shots[4].IsCrit);
        }

        /// <summary>임계 유지 정책(T-H10): 구간 끝까지 쏘고, 열이 100 − 4n 이하로 식으면 다시 쏜다. 돌려주는 값 = 평균 DPS.</summary>
        static double RunCritPolicy(Kit k, int ticks)
        {
            int shot = Kit.BoltHeatHu;
            int n = PlayerRules.CritShotCount(k.P.Derived.CritStartHu, shot);
            int resumeHu = PlayerTuning.HeatMaxHu - shot * n;
            k.P.HeatHu = resumeHu;
            bool firing = false;
            for (int t = 0; t < ticks; t++)
            {
                bool wantFire = firing ? k.P.HeatHu + shot <= PlayerTuning.HeatMaxHu : k.P.HeatHu <= resumeHu;
                if (wantFire != firing)
                {
                    if (wantFire) k.Press(LogicalKey.Fire); else k.Release(LogicalKey.Fire);
                    firing = wantFire;
                }
                k.Step();
            }
            return Damage(k, 0, k.Spells.Shots.Count) / (ticks / 60.0);
        }

        // T-H10: 임계 유지 정책 6,000틱 — 잠금 0회, 모든 발사 임계. 평균 DPS 기준 69.6 / 달구 73.8 / 초롱 71.8 / 성에 65.6.
        // 명세의 틱 값은 정책의 세부(다시 쏘는 틱)에 따라 연속식과 ±1% 안에서 움직인다고 적혀 있어 ±1%로 본다.
        [TestCase(null, 69.6)]
        [TestCase("char.1", 73.8)]
        [TestCase("char.2", 71.8)]
        [TestCase("char.4", 65.6)]
        public void S01_T_H10__CritMaintainPolicy(string charId, double expectedDps)
        {
            var k = Kit.Create(charId);
            double dps = RunCritPolicy(k, 6000);
            Assert.AreEqual(0, k.Count(SimEventType.HeatLockEntered));
            Assert.IsFalse(k.P.Locked);
            for (int i = 0; i < k.Spells.Shots.Count; i++) Assert.IsTrue(k.Spells.Shots[i].IsCrit, "shot " + i);
            Assert.AreEqual(expectedDps, dps, expectedDps * 0.01);
        }

        // T-H11: 임계 유지 ÷ 꾹 누르기 ≥ 1.4 (기준값. 꾹 누르기 44.93 = T-H02).
        [Test]
        public void S01_T_H11__CritPolicyBeatsHoldByFortyPercent()
        {
            double policy = RunCritPolicy(Kit.Create(), 6000);
            double hold = 310 / (414 / 60.0);
            Assert.GreaterOrEqual(policy / hold, 1.4);
        }

        // T-H12: 열 50, 마지막 발사 틱 0(마력탄), J 안 누름 → 틱 0~20 냉각 없음, 틱 21의 끝부터 틱당 0.75. 틱 21 끝 49.25.
        [Test]
        public void S01_T_H12__CoolingStartsAfterIntervalPlusDelay()
        {
            var k = Kit.Create();
            k.P.HeatHu = 50 * Hu;
            k.P.LastFireTick = 0;
            k.P.LastFireIntervalTicks = Kit.BoltIntervalTicks;
            k.StepTo(21);
            Assert.AreEqual(50 * Hu, k.P.HeatHu);
            k.Step();
            Assert.AreEqual(5910, k.P.HeatHu);   // 49.25
            k.Step();
            Assert.AreEqual(5820, k.P.HeatHu);
        }

        // T-H13: 초롱, 열 50 → 틱당 0.8333(100 HU). 60틱 냉각 = 정확히 50.
        [Test]
        public void S01_T_H13__Char2CoolsExactlyFiftyInSixtyTicks()
        {
            var k = Kit.Create("char.2");
            k.P.HeatHu = 50 * Hu;
            k.P.LastFireTick = 0;
            k.P.LastFireIntervalTicks = Kit.BoltIntervalTicks;
            k.StepTo(22);
            Assert.AreEqual(50 * Hu - 100, k.P.HeatHu);
            k.StepTo(21 + 59);
            Assert.AreEqual(100, k.P.HeatHu);
            k.Step();
            Assert.AreEqual(0, k.P.HeatHu);
            k.Step(5);
            Assert.AreEqual(0, k.P.HeatHu, "0 아래로 내려가지 않는다");
        }

        // T-H14: 잠금 중 냉각 없음. T-H15: 잠금 중 J 누름 → 발사 없음, FireDryWhileLocked 1건.
        [Test]
        public void S01_T_H14_H15__LockedNoCoolNoFireDryEventOnPress()
        {
            var k = Kit.Create();
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.P.UnlockTick = 500;
            k.Press(LogicalKey.Fire);
            k.Step(40);
            Assert.AreEqual(104 * Hu, k.P.HeatHu);
            Assert.AreEqual(0, k.Spells.Shots.Count);
            Assert.AreEqual(1, k.Count(SimEventType.FireDryWhileLocked), "누르는 순간에만 한 번");
        }

        // T-H17: 잠금 중 방열 폭발 → 피해 계산용 열 100, 잠금 풀림, 열 0. T-H18: 열 60 → 60, 열 0.
        [Test]
        public void S01_T_H17_H18__ConsumeHeatForVent()
        {
            var k = Kit.Create();
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.P.UnlockTick = 500;
            Assert.AreEqual(100f, k.Sys.ConsumeHeatForVent(k.W, k.P));
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(0, k.P.HeatHu);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatUnlocked, out e));
            Assert.AreEqual((int)HeatUnlockReason.VentBlast, e.A);

            k.P.HeatHu = 60 * Hu;
            Assert.AreEqual(60f, k.Sys.ConsumeHeatForVent(k.W, k.P));
            Assert.AreEqual(0, k.P.HeatHu);
            // 반환 열은 내림.
            k.P.HeatHu = 60 * Hu + 119;
            Assert.AreEqual(60f, k.Sys.ConsumeHeatForVent(k.W, k.P));
        }

        // T-H19~24: 발사 1회의 열. Spells의 컴파일 결과(발사 열)를 가짜로 넣어 TryFire의 열 반영만 본다.
        // 마력탄 + 3갈래 8.8(1,056) / + 2갈래 6.4(768) / 3갈래 둘(둘째 무시) 8.8 / + 거울상 6(720) / 3갈래 + 거울상 13.2(1,584) / 트리거 스코프 화염구 22.
        [TestCase("T-H19", 1056, false, 1056)]
        [TestCase("T-H20", 768, false, 768)]
        [TestCase("T-H21", 1056, false, 1056)]
        [TestCase("T-H22", 480, true, 720)]
        [TestCase("T-H23", 1056, true, 1584)]
        [TestCase("T-H24", 2640, false, 2640)]
        public void S01_T_H19_H24__ShotHeatApplied(string vector, int compiledHu, bool mirror, int expectedHu)
        {
            var k = Kit.Create(null, kit => kit.Traits.Mirror = mirror);
            k.P.Spell = FakeSpells.Make(Kit.BoltIntervalTicks, compiledHu);
            Assert.AreEqual(expectedHu, k.Sys.ShotHeatHu(k.W, k.P));
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(expectedHu, k.P.HeatHu);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ShotFired, out e));
            Assert.AreEqual(expectedHu / 120f, e.F, 1e-4f);
        }

        // 6.6절: 임계 사격 가능 횟수 n = (12000 − critStartHu) / shotHu + 1. 마력탄 85→4, 80→6, 90→3. 열 0이면 무한, 넘치면 0.
        [Test]
        public void S01_7_7__CritShotCount()
        {
            Assert.AreEqual(4, PlayerRules.CritShotCount(85 * Hu, 480));
            Assert.AreEqual(6, PlayerRules.CritShotCount(80 * Hu, 480));
            Assert.AreEqual(3, PlayerRules.CritShotCount(90 * Hu, 480));
            Assert.AreEqual(-1, PlayerRules.CritShotCount(85 * Hu, 0));
            Assert.AreEqual(0, PlayerRules.CritShotCount(85 * Hu, 12001));
            var c0 = Kit.Create();
            Assert.AreEqual(4, c0.Sys.CritShotCount(c0.W, c0.P));
            var c1 = Kit.Create("char.1");
            Assert.AreEqual(6, c1.Sys.CritShotCount(c1.W, c1.P));
            var c4 = Kit.Create("char.4");
            Assert.AreEqual(3, c4.Sys.CritShotCount(c4.W, c4.P));
        }

        // 6.6절 열 링: Cold 0 / Rising < 임계 − 15 / Imminent < 임계 / Critical < 95 / Brink 95~100 / Locked.
        [TestCase(0, HeatRingState.Cold)]
        [TestCase(1, HeatRingState.Rising)]
        [TestCase(8399, HeatRingState.Rising)]
        [TestCase(8400, HeatRingState.Imminent)]      // 70 = 85 − 15
        [TestCase(10199, HeatRingState.Imminent)]
        [TestCase(10200, HeatRingState.Critical)]     // 정확히 85
        [TestCase(11399, HeatRingState.Critical)]
        [TestCase(11400, HeatRingState.Brink)]        // 95
        [TestCase(12000, HeatRingState.Brink)]
        public void S01_7_7__RingStates(int heatHu, HeatRingState expected)
        {
            var k = Kit.Create();
            k.P.HeatHu = heatHu;
            Assert.AreEqual(expected, k.Sys.RingState(k.W, k.P));
            k.P.Locked = true;
            Assert.AreEqual(HeatRingState.Locked, k.Sys.RingState(k.W, k.P));
        }

        // 링 상태는 세계의 모듈이 아닌 다른 PlayerSystem 객체(예: ctx.Rules)로 물어도 같다.
        [Test]
        public void RingState_SameFromAnotherModuleInstance()
        {
            var k = Kit.Create("char.1");
            k.P.HeatHu = 96 * Hu;
            Assert.AreEqual(HeatRingState.Brink, new PlayerSystem().RingState(k.W, k.P));
            k.P.HeatHu = 70 * Hu;   // 달구: 80 − 15 = 65부터 Imminent
            Assert.AreEqual(HeatRingState.Imminent, new PlayerSystem().RingState(k.W, k.P));
        }

        [Test]
        public void HeatRingChanged_EmittedOnTransition()
        {
            var k = Kit.Create();
            k.P.HeatHu = 84 * Hu;
            k.Step();
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatRingChanged, out e));
            Assert.AreEqual((int)HeatRingState.Imminent, e.A);
            k.W.Events.Clear();
            k.Step();
            Assert.AreEqual(0, k.Count(SimEventType.HeatRingChanged));
        }

        // 8.3절 불가마 검산(S04 15.5-71): 달구, 열 76에서 깔고 마력탄 연사 →
        // 1~4발 판정 열 80 임계, 흡수(열 76 유지, 16→0). 5~10발 80, 84, …, 100 평소 임계. 10발 연속 임계, 11발째 104 임계 + 잠금.
        [Test]
        public void S04_15_5_71__KilnAbsorbsCritHeat()
        {
            var k = Kit.Create("char.1");
            k.P.HeatHu = 76 * Hu;
            k.Arts.InKiln = true;
            k.Arts.KilnLeftHu = 16 * Hu;
            k.Press(LogicalKey.Fire);
            int[] heatAfter = { 76, 76, 76, 76, 80, 84, 88, 92, 96, 100, 104 };
            for (int i = 0; i < 11; i++)
            {
                k.StepTo(i * 9 + 1);
                Assert.AreEqual(i + 1, k.Spells.Shots.Count);
                Assert.IsTrue(k.Spells.Shots[i].IsCrit, "shot " + (i + 1));
                Assert.AreEqual(heatAfter[i] * Hu, k.P.HeatHu, "shot " + (i + 1));
                Assert.AreEqual(i < 10, !k.P.Locked, "shot " + (i + 1));
            }
            Assert.AreEqual(0, k.Arts.KilnLeftHu);
            Assert.AreEqual(4, k.Count(SimEventType.KilnAbsorbed));
            Assert.IsTrue(k.P.Locked);
        }

        // 8.3절: 임계가 아닌 발사는 흡수하지 않는다. 남은 양보다 크면 남은 만큼만 먹고 나머지는 오른다.
        [Test]
        public void S04_Kiln__OnlyCritShotsAndPartialAbsorb()
        {
            var k = Kit.Create("char.1");
            k.Arts.InKiln = true;
            k.Arts.KilnLeftHu = 16 * Hu;
            k.P.HeatHu = 10 * Hu;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(14 * Hu, k.P.HeatHu);
            Assert.AreEqual(16 * Hu, k.Arts.KilnLeftHu);

            var b = Kit.Create("char.1");
            b.Arts.InKiln = true;
            b.Arts.KilnLeftHu = 1 * Hu;
            b.P.HeatHu = 80 * Hu;
            b.Press(LogicalKey.Fire);
            b.Step();
            Assert.AreEqual(83 * Hu, b.P.HeatHu);
            Assert.AreEqual(0, b.Arts.KilnLeftHu);
        }

        // 8.3절: 스침 면제가 남아 있으면 면제가 먼저(흡수량 그대로).
        [Test]
        public void S04_Kiln__ExemptGoesFirstAndKeepsAbsorb()
        {
            var k = Kit.Create("char.1");
            k.Arts.InKiln = true;
            k.Arts.KilnLeftHu = 16 * Hu;
            k.P.HeatHu = 90 * Hu;
            k.P.HeatExemptArmed = true;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(90 * Hu, k.P.HeatHu);
            Assert.AreEqual(16 * Hu, k.Arts.KilnLeftHu);
            Assert.IsFalse(k.P.HeatExemptArmed);
            Assert.IsTrue(k.Spells.Shots[0].IsCrit);
            Assert.IsTrue(k.Spells.Shots[0].HeatExempt);
            Assert.AreEqual(1, k.Count(SimEventType.HeatExemptConsumed));
        }

        // 6.3절: 발사 요청의 내용(위치, 방향, 출처, 배율, 발사 번호)과 간격.
        [Test]
        public void S01_7_3__ExecuteRequestAndInterval()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.W;
            k.Press(LogicalKey.Fire);
            k.Step();
            ExecuteRequest r = k.Spells.Shots[0];
            Assert.AreEqual(0, r.OwnerSlot);
            Assert.AreSame(k.P.Spell, r.Tree);
            Assert.AreEqual(k.P.Pos, r.Pos);
            Assert.AreEqual(180f, r.DirDegrees, 1e-4f);
            Assert.AreEqual(1f, r.DamageMul);
            Assert.AreEqual(ShotSource.Fire, r.Source);
            Assert.AreEqual(1, r.ShotId);
            Assert.AreEqual(1f, r.CharacterMul);
            Assert.IsFalse(r.IsCrit);
            Assert.AreEqual(0, k.P.LastFireTick);
            Assert.AreEqual(9, k.P.LastFireIntervalTicks);
            Assert.AreEqual(9, k.P.FireReadyTick);
            k.Step(9);
            Assert.AreEqual(2, k.Spells.Shots[1].ShotId);
            CollectionAssert.AreEqual(new[] { 1, 2 }, k.Arts.FiredShotIds);
            CollectionAssert.AreEqual(new[] { 9, 9 }, k.Traits.FiredIntervals);
        }

        // 00 문서 5.4절 (가): RollUnstableCore → (면제 → 임계 → 불가마 흡수) → Execute → Arts.OnPlayerFired → Traits.OnPlayerFired → 잠금.
        [Test]
        public void Arch_5_4_A__FireCallOrder()
        {
            var k = Kit.Create();
            var log = k.Arts.Log;
            k.Spells.Log = log;
            k.Traits.Log = log;
            k.P.HeatHu = 97 * Hu;
            k.Press(LogicalKey.Fire);
            k.Step();
            CollectionAssert.AreEqual(new[] { "roll", "absorb", "execute", "arts.fired", "traits.fired", "lock" }, log);
        }

        // 계약: RollUnstableCore는 발사 한 번에 정확히 한 번. 누르고만 있는 틱·간격 중에는 0번.
        [Test]
        public void Contract__RollUnstableCoreOncePerShot()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Fire);
            k.Step(30);   // 발사 틱 0, 9, 18, 27
            Assert.AreEqual(4, k.Spells.Shots.Count);
            Assert.AreEqual(4, k.Traits.RollCalls);
            Assert.AreEqual(4, k.Traits.FiredCalls);
        }

        [Test]
        public void Contract__RollUnstableCoreNotCalledWhenShotCannotGoOut()
        {
            // AttackLocked 중
            var a = Kit.Create();
            a.P.AttackLocked = true;
            a.Press(LogicalKey.Fire);
            a.Step(20);
            Assert.AreEqual(0, a.Traits.RollCalls);
            Assert.AreEqual(0, a.Spells.Shots.Count);

            // 과열 잠금 중
            var b = Kit.Create();
            b.P.Locked = true;
            b.P.UnlockTick = 1000;
            b.Press(LogicalKey.Fire);
            b.Step(20);
            Assert.AreEqual(0, b.Traits.RollCalls);

            // 유효하지 않은 주문
            var c = Kit.Create();
            c.P.Spell = FakeSpells.Make(9, 480, false);
            c.Press(LogicalKey.Fire);
            c.Step(20);
            Assert.AreEqual(0, c.Traits.RollCalls);
            Assert.AreEqual(0, c.Spells.Shots.Count);

            // J를 누르지 않음
            var d = Kit.Create();
            d.Step(20);
            Assert.AreEqual(0, d.Traits.RollCalls);
        }

        // 공통 규칙 7: AttackAllowed가 거짓인 단계에서는 발사가 없다(WavePrelude·WaveCombat·Practice만 된다).
        [TestCase(RunPhase.WavePrelude, true)]
        [TestCase(RunPhase.WaveCombat, true)]
        [TestCase(RunPhase.Practice, true)]
        [TestCase(RunPhase.StartCamp, false)]
        [TestCase(RunPhase.Camp, false)]
        [TestCase(RunPhase.BossIntro, false)]
        [TestCase(RunPhase.WaveClearing, false)]
        [TestCase(RunPhase.LootPick, false)]
        [TestCase(RunPhase.PhantomFinale, false)]
        [TestCase(RunPhase.PhantomDowned, false)]
        [TestCase(RunPhase.RunEnded, false)]
        public void Arch_Rule7__FireOnlyWhenAttackAllowed(RunPhase phase, bool fires)
        {
            var k = Kit.Create();
            k.W.Run.Phase = phase;
            k.Press(LogicalKey.Fire);
            k.Step(3);
            Assert.AreEqual(fires ? 1 : 0, k.Spells.Shots.Count);
            Assert.AreEqual(fires ? 1 : 0, k.Traits.RollCalls);
            Assert.AreEqual(fires ? 480 : 0, k.P.HeatHu);
        }

        [Test]
        public void Arch_Rule7__NoFireWhenDownedOrInputLocked()
        {
            var a = Kit.Create();
            a.Press(LogicalKey.Fire);
            a.Step();
            a.P.Downed = true;
            a.Step(20);
            Assert.AreEqual(1, a.Spells.Shots.Count);
        }

        // D-04·D-05: 불안정 핵 자폭 — 탄이 나가지 않고 열도 오르지 않는다. 잠금 = t + 간격 + 120틱, 콤보 0.
        [Test]
        public void S01_7_6_D05__UnstableCoreSelfDestruct()
        {
            var k = Kit.Create();
            k.Traits.NextUnstable = true;
            k.StepTo(5);
            k.P.HeatHu = 30 * Hu;
            k.P.Combo = 7;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(0, k.Spells.Shots.Count);
            Assert.AreEqual(0, k.Count(SimEventType.ShotFired));
            Assert.AreEqual(30 * Hu, k.P.HeatHu);
            Assert.IsTrue(k.P.Locked);
            Assert.AreEqual(5 + 9 + 120, k.P.UnlockTick);
            Assert.AreEqual(0, k.P.Combo);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboReset, out e));
            Assert.AreEqual((int)ComboResetReason.UnstableCore, e.A);
            Assert.AreEqual(7, e.B);
            CollectionAssert.AreEqual(new[] { true }, k.Traits.LockEntered, "끓어넘침 여부는 Traits가 fromUnstableCore로 가른다");
            Assert.AreEqual(1, k.Traits.RollCalls);
            Assert.AreEqual(1, k.Traits.FiredCalls, "자폭 발사도 Traits.OnPlayerFired를 부른다");
            Assert.AreEqual(0, k.Arts.FiredShotIds.Count, "탄이 없으니 사본 마법진은 따라 쏘지 않는다");
            // 2초 + 간격 뒤 풀리고 열 0.
            k.Traits.NextUnstable = false;
            k.StepTo(5 + 9 + 120);
            Assert.IsTrue(k.P.Locked);
            k.Step();
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(1, k.Spells.Shots.Count, "풀린 틱에 바로 다음 발사");
        }

        // 6.5절·D-04: 이미 잠겨 있으면 더 긴 쪽.
        [Test]
        public void S01_7_5__EnterLockKeepsLongerAndHooksOnce()
        {
            var k = Kit.Create();
            k.StepTo(10);
            k.Sys.EnterLock(k.W, k.P, 9, 180, false);
            Assert.AreEqual(199, k.P.UnlockTick);
            k.Sys.EnterLock(k.W, k.P, 9, 120, true);
            Assert.AreEqual(199, k.P.UnlockTick);
            k.Sys.EnterLock(k.W, k.P, 36, 180, false);
            Assert.AreEqual(226, k.P.UnlockTick);
            Assert.AreEqual(1, k.Traits.LockEntered.Count);
            Assert.AreEqual(1, k.Count(SimEventType.HeatLockEntered));
        }

        [Test]
        public void S01_7_5__BreakLockSetsHeat()
        {
            var k = Kit.Create();
            k.P.Locked = true;
            k.P.UnlockTick = 300;
            k.P.HeatHu = 104 * Hu;
            k.Sys.BreakLock(k.W, k.P, HeatUnlockReason.Graze, 85 * Hu);
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(85 * Hu, k.P.HeatHu);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.HeatUnlocked, out e));
            Assert.AreEqual((int)HeatUnlockReason.Graze, e.A);
        }

        // 탐식용: 열 깎기. 잠금 중이면 아무 일도 없다.
        [Test]
        public void S01_7_6__ReduceHeatNotWhileLocked()
        {
            var k = Kit.Create();
            k.P.HeatHu = 50 * Hu;
            k.Sys.ReduceHeat(k.W, k.P, 12 * Hu);
            Assert.AreEqual(38 * Hu, k.P.HeatHu);   // T-H28: 50 → 38
            k.Sys.ReduceHeat(k.W, k.P, 100 * Hu);
            Assert.AreEqual(0, k.P.HeatHu);
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.Sys.ReduceHeat(k.W, k.P, 4 * Hu);
            Assert.AreEqual(104 * Hu, k.P.HeatHu);
        }

        // D-57 연습 옵션 "열 고정": 발사·냉각으로 열이 변하지 않고 잠금에 들어가지 않는다.
        [Test]
        public void D57__PracticeHeatFreeze()
        {
            var k = Kit.Create();
            k.W.Run.Phase = RunPhase.Practice;
            k.W.Run.PracticeHeatFreeze = true;
            k.P.HeatHu = 98 * Hu;
            k.Press(LogicalKey.Fire);
            k.Step(100);
            Assert.AreEqual(12, k.Spells.Shots.Count);
            Assert.AreEqual(98 * Hu, k.P.HeatHu);
            Assert.IsFalse(k.P.Locked);
            for (int i = 0; i < 12; i++) Assert.IsTrue(k.Spells.Shots[i].IsCrit);
            k.Release(LogicalKey.Fire);
            k.Step(200);
            Assert.AreEqual(98 * Hu, k.P.HeatHu, "식지도 않는다");
            // 끄면 다시 평소대로.
            k.W.Run.PracticeHeatFreeze = false;
            k.Step();
            Assert.Less(k.P.HeatHu, 98 * Hu);
        }

        [Test]
        public void D57__HeatFreezeBlocksUnstableCoreLockToo()
        {
            var k = Kit.Create();
            k.W.Run.Phase = RunPhase.Practice;
            k.W.Run.PracticeHeatFreeze = true;
            k.Traits.NextUnstable = true;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.IsFalse(k.P.Locked);
        }

        // 1단계: 과열 잠금 풀림은 t ≥ UnlockTick인 틱의 맨 앞.
        [Test]
        public void S01_7_5__UnlockAtExactTick()
        {
            var k = Kit.Create();
            k.P.Locked = true;
            k.P.HeatHu = 104 * Hu;
            k.P.UnlockTick = 3;
            k.Step(3);
            Assert.IsTrue(k.P.Locked);
            k.Step();
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(0, k.P.HeatHu);
        }
    }
}
