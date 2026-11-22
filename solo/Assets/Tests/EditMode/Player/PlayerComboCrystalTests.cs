using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Player;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>콤보(10 요약서 1절 규칙 1~8)와 마력 결정(10 요약서 2절).</summary>
    public class PlayerComboCrystalTests
    {
        static void Hit(Kit k, int shotId, bool crit = false) => k.Sys.OnPlayerHitEnemy(k.W, Kit.FireHit(shotId, crit));

        // ------------------------------------------------------------ 콤보

        // 규칙 1: 적중 +1, 임계 적중 +2. 발사 1회로 오르는 콤보는 최대 +3.
        [Test]
        public void S01_9_Rule1__HitPlusOneCritPlusTwoCapThreePerShot()
        {
            var k = Kit.Create();
            Hit(k, 1);
            Assert.AreEqual(1, k.P.Combo);
            Hit(k, 2, true);
            Assert.AreEqual(3, k.P.Combo);
            // 같은 발사(관통·폭발·연쇄로 여러 적중): +3까지만.
            for (int i = 0; i < 6; i++) Hit(k, 3);
            Assert.AreEqual(6, k.P.Combo);
            // 임계 발사의 여러 적중: 2 + 1(상한 3에 맞춰 잘림).
            Hit(k, 4, true);
            Hit(k, 4, true);
            Hit(k, 4, true);
            Assert.AreEqual(9, k.P.Combo);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboChanged, out e));
            Assert.AreEqual(9, e.A);
            Assert.AreEqual(1, e.B);
        }

        // 규칙 2: 2초(120틱) 동안 못 맞히면 0.
        [Test]
        public void S01_9_Rule2__TimeoutAfter120Ticks()
        {
            var k = Kit.Create();
            Hit(k, 1);
            k.Step(119);
            Assert.AreEqual(1, k.P.Combo);
            k.Step();
            Assert.AreEqual(0, k.P.Combo);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboReset, out e));
            Assert.AreEqual((int)ComboResetReason.Timeout, e.A);
        }

        // 규칙 2: 과열 잠금 중에는 타이머 정지.
        [Test]
        public void S01_9_Rule2__TimerPausedWhileLocked()
        {
            var k = Kit.Create();
            Hit(k, 1);
            k.Step(100);
            k.P.Locked = true;
            k.P.UnlockTick = 400;
            k.StepTo(400);
            Assert.AreEqual(1, k.P.Combo);
            Assert.AreEqual(100, k.P.ComboIdleTicks);
            k.Step(19);   // 틱 400에 풀리고 다시 센다
            Assert.AreEqual(1, k.P.Combo);
            k.Step();
            Assert.AreEqual(0, k.P.Combo);
        }

        // 1.2절: 상한에 걸린 적중도 타이머는 다시 채운다 [임시].
        [Test]
        public void S01_9_2__CappedHitStillRefillsTimer()
        {
            var k = Kit.Create();
            // 그 발사의 탄(관통 중)이 아직 살아 있다.
            k.W.Projectiles.Add(new ProjectileEntity { Id = 70, OwnerSlot = 0, ShotId = 1 });
            Hit(k, 1); Hit(k, 1); Hit(k, 1);
            Assert.AreEqual(3, k.P.Combo);
            k.Step(100);
            Hit(k, 1);
            Assert.AreEqual(3, k.P.Combo);
            Assert.AreEqual(0, k.P.ComboIdleTicks);
        }

        // 규칙 3: 콤보 15마다 결정 1개(제자 2는 12마다). 15, 30, 45…를 "지날 때".
        [TestCase(null, 15)]
        [TestCase("char.1", 15)]
        [TestCase("char.2", 12)]
        [TestCase("char.5", 15)]
        public void S01_9_Rule3__CrystalEveryNCombo(string charId, int per)
        {
            var k = Kit.Create(charId);
            for (int i = 1; i <= per - 1; i++) Hit(k, i);
            Assert.AreEqual(per - 1, k.P.Combo);
            Assert.AreEqual(0, k.P.Crystals);
            Hit(k, 100);
            Assert.AreEqual(per, k.P.Combo);
            Assert.AreEqual(1, k.P.Crystals);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.CrystalGained, out e));
            Assert.AreEqual(1, e.A);
            Assert.AreEqual((int)CrystalSource.Combo, e.B);
            // 경계를 한 번에 넘어도(14 → 16) 1개, 두 경계를 넘으면 2개.
            k.Sys.AddCombo(k.W, k.P, per * 2);
            Assert.AreEqual(3, k.P.Crystals);
        }

        [Test]
        public void S01_9_Rule3__CritCrossingBoundaryGivesOne()
        {
            var k = Kit.Create();
            k.Sys.AddCombo(k.W, k.P, 14);
            Assert.AreEqual(0, k.P.Crystals);
            Hit(k, 1, true);
            Assert.AreEqual(16, k.P.Combo);
            Assert.AreEqual(1, k.P.Crystals);
        }

        // 1.2절: 결정이 상한이면 결정은 얻지 못하고 콤보는 계속 오른다 [해석].
        [Test]
        public void S01_9_2__ComboKeepsRisingAtCrystalCap()
        {
            var k = Kit.Create();   // 기준 상한 5
            k.P.Crystals = 5;
            k.Sys.AddCombo(k.W, k.P, 15);
            Assert.AreEqual(15, k.P.Combo);
            Assert.AreEqual(5, k.P.Crystals);
            Assert.AreEqual(1, k.Count(SimEventType.CrystalGainBlocked));
        }

        // 규칙 4: 피격 시 0 → PlayerHitTests.S01_6_2__HitResetsCombo.

        // 규칙 5: 비기로 맞힌 것은 넣지 않는다. 규칙 7: 사본 마법진 적중, 피뢰침 벼락(패시브), 지대는 넣지 않는다.
        [Test]
        public void S01_9_Rule5_Rule7__ArtCopyPassiveZoneExcluded()
        {
            var k = Kit.Create();
            var h = Kit.FireHit(1);
            h.Origin = DamageOrigin.Art;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            h = Kit.FireHit(2); h.Origin = DamageOrigin.Passive;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            h = Kit.FireHit(3); h.Origin = DamageOrigin.Zone;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            h = Kit.FireHit(4); h.ShotSource = ShotSource.CopyCircle;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            h = Kit.FireHit(5); h.ShotSource = ShotSource.ManaDischarge;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            h = Kit.FireHit(6); h.ShotSource = ShotSource.ArtOther;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(0, k.P.ShotComboGain.Count);
            Assert.AreEqual(0, k.Count(SimEventType.ComboChanged));
        }

        // 규칙 5: 넣지 않는 적중은 끊김 타이머도 채우지 않는다.
        [Test]
        public void S01_9_Rule5__ExcludedHitDoesNotRefillTimer()
        {
            var k = Kit.Create();
            Hit(k, 1);
            k.Step(50);
            var h = Kit.FireHit(2); h.Origin = DamageOrigin.Art;
            k.Sys.OnPlayerHitEnemy(k.W, h);
            Assert.AreEqual(50, k.P.ComboIdleTicks);
        }

        // 규칙 6: 스쳐 피하기 1회 인정 = +2 (발사 +3 상한과 무관).
        [Test]
        public void S01_9_Rule6__GrazePlusTwo()
        {
            var k = Kit.Create();
            Hit(k, 1); Hit(k, 1); Hit(k, 1);
            k.Sys.AwardGraze(k.W, k.P, 1, GrazeKind.Bullet, k.P.Pos);
            Assert.AreEqual(5, k.P.Combo);
        }

        // 규칙 8: 불안정 핵 자폭 = 0 → PlayerHeatTests.S01_7_6_D05__UnstableCoreSelfDestruct.

        // 1.2절: Reset은 발사별 기록도 지운다. shotComboGain은 그 발사의 탄이 모두 사라지면 지운다 [임시].
        [Test]
        public void S01_9_2__ShotGainForgottenWhenProjectilesGone()
        {
            var k = Kit.Create();
            var proj = new ProjectileEntity { Id = 70, OwnerSlot = 0, ShotId = 1 };
            k.W.Projectiles.Add(proj);
            Hit(k, 1);
            Hit(k, 2);
            Assert.AreEqual(2, k.P.ShotComboGain.Count);
            k.Step();
            Assert.IsTrue(k.P.ShotComboGain.ContainsKey(1), "탄이 살아 있는 발사는 남긴다");
            Assert.IsFalse(k.P.ShotComboGain.ContainsKey(2));
            proj.Dead = true;
            k.Step();
            Assert.AreEqual(0, k.P.ShotComboGain.Count);
            Assert.AreEqual(2, k.P.Combo);

            Hit(k, 5);
            k.Sys.ResetCombo(k.W, k.P, ComboResetReason.WaveEnd);
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(0, k.P.ComboIdleTicks);
            Assert.AreEqual(0, k.P.ShotComboGain.Count);
        }

        // ------------------------------------------------------------ 결정

        // 2.2절 Gain: 상한(5/5/4/6/7)까지. 가득이면 CrystalsFull, 넘치면 CrystalGainBlocked + false.
        [TestCase("char.1", 5)]
        [TestCase("char.2", 5)]
        [TestCase("char.3", 4)]
        [TestCase("char.4", 6)]
        [TestCase("char.5", 7)]
        public void S01_10_Rule1__GainUpToCap(string charId, int cap)
        {
            var k = Kit.Create(charId);
            for (int i = 1; i <= cap; i++)
            {
                Assert.IsTrue(k.Sys.GainCrystal(k.W, k.P, CrystalSource.Starfall));
                Assert.AreEqual(i, k.P.Crystals);
            }
            Assert.AreEqual(1, k.Count(SimEventType.CrystalsFull));
            Assert.AreEqual(cap, k.Count(SimEventType.CrystalGained));
            Assert.IsFalse(k.Sys.GainCrystal(k.W, k.P, CrystalSource.Starfall), "상한이면 false(별똥은 남겨 둔다)");
            Assert.AreEqual(cap, k.P.Crystals);
            Assert.AreEqual(1, k.Count(SimEventType.CrystalGainBlocked));
        }

        // 2.2절 Gain: 빚이 있으면 빚부터 갚는다(결정은 늘지 않는다).
        [Test]
        public void S01_10_3__GainRepaysDebtFirst()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Debt = 2;
            Assert.IsTrue(k.Sys.GainCrystal(k.W, k.P, CrystalSource.Combo));
            Assert.AreEqual(1, k.P.Debt);
            Assert.AreEqual(0, k.P.Crystals);
            Assert.IsTrue(k.Sys.GainCrystal(k.W, k.P, CrystalSource.Starfall));
            Assert.AreEqual(0, k.P.Debt);
            Assert.AreEqual(0, k.P.Crystals);
            Assert.AreEqual(2, k.Count(SimEventType.DebtRepaid));
            Assert.IsTrue(k.Sys.GainCrystal(k.W, k.P, CrystalSource.Combo));
            Assert.AreEqual(1, k.P.Crystals);
        }

        // 2.2절 CanAfford: 결정이 넉넉하면 참. 모자라면 외상(제자 5)만: 빚 + 모자란 수 ≤ 2.
        [Test]
        public void S01_10_2__CanAfford()
        {
            var plain = Kit.Create();
            plain.P.Crystals = 2;
            Assert.IsTrue(plain.Sys.CanAfford(plain.W, plain.P, 2, true));
            Assert.IsFalse(plain.Sys.CanAfford(plain.W, plain.P, 3, true), "외상 없는 캐릭터");

            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            Assert.IsTrue(k.Sys.CanAfford(k.W, k.P, 2, true), "결정 0·빚 0 → 2개 외상");
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 3, true));
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 1, false), "만개처럼 외상을 허용하지 않는 비기");
            k.P.Crystals = 1;
            Assert.IsTrue(k.Sys.CanAfford(k.W, k.P, 3, true));
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 4, true));
            k.P.Crystals = 0;
            k.P.Debt = 2;
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 1, true), "빚 2면 외상 불가");
        }

        // 02 요약서 8.3절의 예: 결정 0·빚 1에서 2개짜리 고유 비기 → 외상 불가 → 부모(1개)가 외상으로, 빚 2.
        [Test]
        public void S04_7_4__CreditExample_UniqueFallsBackToParentOnCredit()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Debt = 1;
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 2, true));
            Assert.IsTrue(k.Sys.CanAfford(k.W, k.P, 1, true));
            Assert.AreEqual(1, k.Sys.SpendCrystals(k.W, k.P, 1, true));
            Assert.AreEqual(2, k.P.Debt);
            Assert.AreEqual(0, k.P.Crystals);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.DebtAdded, out e));
            Assert.AreEqual(2, e.A);
        }

        // 2.2절 Spend: use = min(결정, 값), 모자란 만큼 빚.
        [Test]
        public void S01_10_2__Spend()
        {
            var k = Kit.Create();
            k.P.Crystals = 4;
            Assert.AreEqual(0, k.Sys.SpendCrystals(k.W, k.P, 2, false));
            Assert.AreEqual(2, k.P.Crystals);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.CrystalSpent, out e));
            Assert.AreEqual(2, e.A);

            var c = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            c.P.Crystals = 1;
            Assert.AreEqual(1, c.Sys.SpendCrystals(c.W, c.P, 2, true));
            Assert.AreEqual(0, c.P.Crystals);
            Assert.AreEqual(1, c.P.Debt);
        }

        // 2.2절 SpendAll(만개): 전부 쓴다. 외상 없음.
        [Test]
        public void S01_10_2__SpendAll()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Crystals = 5;
            Assert.AreEqual(5, k.Sys.SpendAllCrystals(k.W, k.P));
            Assert.AreEqual(0, k.P.Crystals);
            Assert.AreEqual(0, k.P.Debt);
            Assert.AreEqual(0, k.Sys.SpendAllCrystals(k.W, k.P));
        }

        // 08 요약서 8.5절 연습 옵션 "결정 무한": CanAfford가 늘 참이고 써도 줄지 않는다. 만개는 지금 가진 수만큼.
        [Test]
        public void S13_8_5__PracticeInfiniteCrystal()
        {
            var k = Kit.Create();
            k.W.Run.Phase = RunPhase.Practice;
            k.W.Run.PracticeInfiniteCrystal = true;
            k.P.Crystals = 3;
            Assert.IsTrue(k.Sys.CanAfford(k.W, k.P, 9, false));
            Assert.AreEqual(0, k.Sys.SpendCrystals(k.W, k.P, 2, true));
            Assert.AreEqual(3, k.P.Crystals);
            Assert.AreEqual(0, k.P.Debt);
            Assert.AreEqual(3, k.Sys.SpendAllCrystals(k.W, k.P));
            Assert.AreEqual(3, k.P.Crystals);
            // 세계의 모듈이 아닌 다른 객체로 물어도 같다(값은 넘긴 세계에서 읽는다).
            Assert.IsTrue(new PlayerSystem().CanAfford(k.W, k.P, 9, false));
            // 끄면 평소대로.
            k.W.Run.PracticeInfiniteCrystal = false;
            Assert.IsFalse(k.Sys.CanAfford(k.W, k.P, 9, false));
            Assert.AreEqual(0, k.Sys.SpendCrystals(k.W, k.P, 2, false));
            Assert.AreEqual(1, k.P.Crystals);
        }

        // 2.1절 7: 그림자 계약의 배율은 Traits가 준다. J 발사는 "발사 순간 가진 결정 수"로 묻는다.
        [Test]
        public void S04_4_5__FireAsksCharacterMulWithHeldCrystals()
        {
            var probe = new MulProbe();
            var k = Kit.Create("char.5");
            k.W.Mods.Traits = probe;
            k.P.Crystals = 4;
            k.Press(LogicalKey.Fire);
            k.Step();
            Assert.AreEqual(4, probe.LastCrystals);
            Assert.AreEqual(1.12f, k.Spells.Shots[0].CharacterMul, 1e-5f);
        }

        sealed class MulProbe : TraitModule
        {
            public int LastCrystals = -1;
            public override float CharacterMul(SimWorld w, PlayerState p, int crystalsHeld)
            {
                LastCrystals = crystalsHeld;
                // 02 요약서 8.3절: 1 + 0.03 × k (가짜가 돌려주는 값. 진짜 규칙은 Traits 모듈의 것이다).
                return 1f + 0.03f * crystalsHeld;
            }
        }
    }
}
